using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs;

/// <summary>
/// MAÇ SONRASI VİDEO TOPLAMA — bitmiş maç ekranının tek besleyicisi.
///
/// NE YAPAR: bitmiş maçların RESMÎ video kayıtlarını önceden toplayıp kanonik DB'ye
/// yazar. Kullanıcı maç detayına tıkladığında ekran yalnız DB'den okur; TIKLAMA BAŞINA
/// 0 dış istek üretilir.
///
/// KAPSAM (daraltma, genişletme değil):
///  • YALNIZ <c>Status=Finished</c> — canlı yoklama YOKTUR.
///  • YALNIZ 11 kilitli organizasyon (<see cref="LockedCompetitions"/>).
///  • api-football kotasına DOKUNULMAZ: video araması bambaşka kaynaklara gider.
///
/// TEKRAR TAKVİMİ — ölçülmüş bir gerçeğe dayanır: resmî özet maçtan ~30-40 dk sonra
/// yayımlanır, ama yayıncıya göre saatler de sürebilir. Bu yüzden üç kez bakılır:
///   1) maç bitiminden ~75 dk sonra   2) ~6 saat sonra   3) ~24 saat sonra
/// Üçü de boş dönerse arama BİTER ve sonuç dürüstçe "Unavailable" olarak yazılır.
/// Sonsuza dek yoklamak, bulunmayan videoyu var etmez; yalnız dış istek harcar.
///
/// KALICI DEFTER: <c>FixtureRefreshAttempts</c> (amaç <c>PostMatchVideo</c>) yeniden
/// kullanılır, yeni tablo açılmaz. RESTART bu defteri sıfırlamaz — süreç belleğinde
/// tutulan bir sayaç, her açılışta aynı isteği yeniden yaptırırdı.
/// </summary>
public sealed class PostMatchEnrichmentJob : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(30);

    /// <summary>Maç bitişinden ilk bakışa kadar geçen süre.</summary>
    public static readonly TimeSpan FirstCheckAfterFullTime = TimeSpan.FromMinutes(75);

    /// <summary>İlk bakış boş dönerse sırasıyla beklenecek süreler.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryBackoff = new[]
    {
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24)
    };

    /// <summary>Toplam deneme hakkı: ilk bakış + iki tekrar.</summary>
    public static readonly int MaxAttempts = 1 + RetryBackoff.Count;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostMatchEnrichmentJob> _logger;

    public PostMatchEnrichmentJob(IServiceScopeFactory scopeFactory, ILogger<PostMatchEnrichmentJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[POST-MATCH VIDEO] Job started.");
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "[POST-MATCH VIDEO] Cycle failed."); }

            try { await Task.Delay(LoopDelay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// TEKRAR TAKVİMİNİN KARARI — saf fonksiyon, testten doğrudan çağrılır.
    /// </summary>
    /// <param name="attemptsSoFar">Bugüne kadarki toplam deneme (gün sınırından bağımsız).</param>
    /// <param name="lastAttemptUtc">En son deneme anı; hiç denenmediyse null.</param>
    public static bool IsDue(DateTime matchEndUtc, int attemptsSoFar, DateTime? lastAttemptUtc, DateTime nowUtc)
    {
        if (attemptsSoFar >= MaxAttempts) return false;                 // hak bitti
        if (attemptsSoFar == 0) return nowUtc >= matchEndUtc + FirstCheckAfterFullTime;
        if (lastAttemptUtc == null) return true;                        // sayaç var, an yok → dene
        return nowUtc >= lastAttemptUtc.Value + RetryBackoff[Math.Min(attemptsSoFar - 1, RetryBackoff.Count - 1)];
    }

    /// <summary>Bir tur — public; testten ve elle tetikten çağrılabilir.</summary>
    public async Task<int> RunCycleAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<FormaxDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var registrar = sp.GetRequiredService<IMatchVideoRegistrar>();
        var provider = sp.GetRequiredService<IOfficialMatchVideoProvider>();
        var repo = sp.GetRequiredService<IFixtureSyncRepository>();

        if (!config.GetValue("PostMatch:Video:Enabled", true)) return 0;

        var maxMatches = Math.Max(0, config.GetValue("PostMatch:Video:MaxMatchesPerCycle", 10));
        if (maxMatches == 0) return 0;

        var dailyCap = Math.Max(0, config.GetValue("PostMatch:Video:MaxRunsPerUtcDay", 200));
        var nowUtc = DateTime.UtcNow;

        var candidates = await BuildCandidatesAsync(db, config, nowUtc, ct).ConfigureAwait(false);
        if (candidates.Count == 0) return 0;

        var processed = 0;
        foreach (var m in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (processed >= maxMatches) break;

            var extId = m.ExternalMatchId!;
            var matchEnd = m.MatchDate + Application.Services.PostMatch.MatchVideoIdentityValidator.MatchDuration;

            // Zaten oynatılabilir bir kaydı varsa iş bitmiştir; aramaya devam edilmez.
            var alreadyPlayable = await db.MatchVideos.AsNoTracking()
                .AnyAsync(v => v.MatchId == m.Id && v.CanPlayInApp, ct).ConfigureAwait(false);
            if (alreadyPlayable) continue;

            // KALICI DEFTER — gün sınırından bağımsız TOPLAM deneme sayısı okunur.
            var ledger = await db.FixtureRefreshAttempts.AsNoTracking()
                .Where(a => a.ExternalMatchId == extId && a.Purpose == FixtureRefreshPurposes.PostMatchVideo)
                .Select(a => new { a.AttemptCount, a.LastAttemptUtc })
                .ToListAsync(ct).ConfigureAwait(false);

            var attempts = ledger.Sum(a => a.AttemptCount);
            DateTime? last = ledger.Count == 0 ? null : ledger.Max(a => a.LastAttemptUtc);

            if (!IsDue(matchEnd, attempts, last, nowUtc)) continue;

            // Atomik rezervasyon: iki süreç aynı maçı aynı anda aramaz.
            if (!repo.TryReserveFixtureAttempt(
                    extId, FixtureRefreshPurposes.PostMatchVideo,
                    TimeSpan.FromHours(1), dailyCap, nowUtc))
                continue;

            var stored = 0;
            var outcome = "NoData";
            try
            {
                var identity = await registrar.BuildIdentityAsync(m.Id, ct).ConfigureAwait(false);
                if (identity != null)
                {
                    foreach (var candidate in await provider.DiscoverAsync(identity, ct).ConfigureAwait(false))
                    {
                        var result = await registrar.RegisterAsync(m.Id, candidate, ct).ConfigureAwait(false);
                        if (result.Stored) stored++;
                    }
                }
                if (stored > 0) outcome = "Applied";
                else if (attempts + 1 >= MaxAttempts)
                    // Hak bitti: "aradık, resmî video bulunamadı" DÜRÜST sonucu saklanır.
                    outcome = MatchVideoVerificationStatuses.Unavailable;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                outcome = "ProviderError";
                _logger.LogWarning(ex, "[POST-MATCH VIDEO] {MatchId} icin arama basarisiz.", m.Id);
            }

            repo.RecordFixtureAttemptOutcome(extId, FixtureRefreshPurposes.PostMatchVideo, nowUtc, outcome);
            processed++;
        }

        await repo.SaveChangesAsync(ct).ConfigureAwait(false);
        if (processed > 0)
            _logger.LogInformation("[POST-MATCH VIDEO] {Count} mac icin video arandi.", processed);
        return processed;
    }

    /// <summary>
    /// ADAYLAR — bitmiş, kilitli kapsamda ve HÂLÂ tekrar penceresi içinde olan maçlar.
    ///
    /// Pencere, tekrar takviminin son adımına göre belirlenir: 24 saatlik son denemeden
    /// sonra bir maç bir daha aranmaz, dolayısıyla listeye de alınmaz.
    /// </summary>
    private static async Task<List<Domain.Entities.Match>> BuildCandidatesAsync(
        FormaxDbContext db, IConfiguration config, DateTime nowUtc, CancellationToken ct)
    {
        var locked = LockedCompetitions.All.ToList();
        var lookbackHours = Math.Max(48, config.GetValue("PostMatch:Video:LookbackHours", 72));
        var since = nowUtc.AddHours(-lookbackHours);

        // Takip edilen maçlar öne alınır (aynı maç kaç kullanıcıda olursa olsun BİR kez).
        var followedIds = await db.UserMatchFollows.AsNoTracking()
            .Select(f => f.MatchId).Distinct().ToListAsync(ct).ConfigureAwait(false);

        var rows = await db.Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatuses.Finished
                     && m.MatchDate >= since && m.MatchDate <= nowUtc
                     && locked.Contains(m.LeagueId)
                     && m.ExternalMatchId != null && m.ExternalMatchId != "")
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .OrderByDescending(m => followedIds.Contains(m.Id))
            .ThenByDescending(m => m.MatchDate)
            .ToList();
    }
}
