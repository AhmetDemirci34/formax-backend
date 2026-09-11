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
/// yayımlanır, ama yayıncıya göre saatler de sürebilir. Bu yüzden DÖRT kez bakılır:
///   FT+60dk → FT+3sa → FT+6sa → FT+24sa (takvim: PostMatchVideoSchedule).
/// Dördü de boş dönerse arama BİTER ve sonuç dürüstçe "Unavailable" olarak yazılır.
/// Plan/rate limit engeli ya da erişilemeyen kaynak deneme SAYILMAZ.
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

    /// <summary>
    /// Maç bitişinden ilk bakışa kadar geçen süre.
    ///
    /// 75 dk → 60 dk (06.09.2026, ürün takvimi): resmî özet çoğu yayıncıda son
    /// düdükten ~30-40 dk sonra yayımlanıyor; ilk bakışı 75 dakikaya çekmek, hazır
    /// olan videoyu gereksiz yere geciktiriyordu.
    /// </summary>
    public static readonly TimeSpan FirstCheckAfterFullTime =
        Application.Services.PostMatch.PostMatchVideoSchedule.FirstCheckAfterFullTime;

    /// <summary>
    /// İlk bakış boş dönerse sırasıyla beklenecek süreler. Toplam takvim:
    /// FT+60dk → FT+3sa → FT+6sa → FT+24sa. Dördüncü deneme de boş dönerse arama
    /// BİTER ve sonuç dürüstçe "Unavailable" yazılır — sonsuza dek yoklamak,
    /// bulunmayan videoyu var etmez.
    ///
    /// Aralıklar bir öncekinin ÜZERİNE eklenir: 60dk + 2sa = FT+3sa, + 3sa = FT+6sa,
    /// + 18sa = FT+24sa.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryBackoff =
        Application.Services.PostMatch.PostMatchVideoSchedule.RetryBackoff;

    /// <summary>Takvime başlamış maçın kalan haklarının kullanılabileceği en geriye bakış.</summary>
    public static readonly TimeSpan StartedScheduleCeiling = TimeSpan.FromDays(30);

    /// <summary>Engellenen turun defter kodları (kolon 32 karakter). Bunlar deneme SAYILMAZ.</summary>
    public const string BlockedRateLimited = "Blocked:RateLimited";
    public const string BlockedUnreachable = "Blocked:Unreachable";
    public const string BlockedError = "Blocked:Error";

    /// <summary>Toplam deneme hakkı: ilk bakış + üç tekrar (takvimin tek kaynağı Application katmanıdır).</summary>
    public static readonly int MaxAttempts = Application.Services.PostMatch.PostMatchVideoSchedule.MaxAttempts;

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
        => Application.Services.PostMatch.PostMatchVideoSchedule.IsDue(matchEndUtc, attemptsSoFar, lastAttemptUtc, nowUtc);

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
        var videoLog = sp.GetService<Telemetry.VideoDiscoveryRequestLog>();

        // ── AŞAMA 1: OLAY + İSTATİSTİK ────────────────────────────────────────
        //
        // AYRI BİR JOB YIĞINI KURULMADI (ürün kararı 06.09.2026): veri toplama, zaten
        // var olan bu turun bir aşamasıdır. Kendi bütçesi, kendi kalıcı defteri ve
        // kendi aday kuralları vardır; video aşamasından bağımsız çalışır ve birinin
        // hatası diğerini durdurmaz.
        try
        {
            await sp.GetRequiredService<PostMatch.PostMatchDataIngestionService>()
                .RunCycleAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[POST-MATCH DATA] veri toplama asamasi basarisiz.");
        }

        // ── AŞAMA 1B: KULLANICI SEÇİMİ SONUÇLANDIRMA ──────────────────────────
        //
        // Aynı turun bir aşamasıdır (video ve veri aşamalarıyla aynı gerekçe): bitmiş
        // maçın seçimleri KALICI olarak sonuçlanır. Sıfır dış istek; yalnız depo.
        // Hatası diğer aşamaları durdurmaz.
        try
        {
            await sp.GetRequiredService<Picks.UserPickSettlementService>()
                .RunCycleAsync(null, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PICK SETTLEMENT] sonuclandirma asamasi basarisiz.");
        }

        // ── AŞAMA 2: RESMÎ VİDEO ──────────────────────────────────────────────
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
            var blocked = false;
            try
            {
                var identity = await registrar.BuildIdentityAsync(m.Id, ct).ConfigureAwait(false);
                if (identity != null)
                {
                    var found = await provider.DiscoverAsync(identity, ct).ConfigureAwait(false);

                    // TUR TAMAMLANDI MI? Hiçbir yapılandırılmış sağlayıcı aramasını hatasız
                    // bitiremediyse (rate limit / plan / erişilemeyen kaynak) bu bir
                    // "bulunamadı" DEĞİLDİR: deneme SAYILMAZ.
                    if (provider is IVideoDiscoveryDiagnostics diag && !diag.LastRunCompleted)
                    {
                        blocked = true;
                        // LastOutcome kolonu 32 karakter: kısa SABİT kod yazılır; sağlayıcı
                        // bazındaki ayrıntı video istek kaydında ve logda durur.
                        outcome = diag.LastOutcomes.Any(o => o.Note.StartsWith("rate-limit", StringComparison.Ordinal))
                            ? BlockedRateLimited
                            : BlockedUnreachable;
                        _logger.LogWarning("[POST-MATCH VIDEO] {MatchId} turu engellendi (deneme sayilmadi): {Detail}",
                            m.Id, string.Join(" | ", diag.LastOutcomes.Select(o => o.Provider + "=" + o.Note)));
                    }

                    foreach (var candidate in found)
                    {
                        var result = await registrar.RegisterAsync(m.Id, candidate, ct).ConfigureAwait(false);
                        if (result.Stored) stored++;
                        videoLog?.RecordVerdict(new Telemetry.VideoDiscoveryRequestLog.VerdictEntry(
                            DateTime.UtcNow, candidate.ProviderName, m.Id, extId,
                            candidate.SourceIdentifier, candidate.ExternalVideoId, candidate.Title,
                            result.Stored, result.Status, result.Reason));
                    }
                }
                if (stored > 0) { outcome = "Applied"; blocked = false; }
                else if (!blocked && attempts + 1 >= MaxAttempts)
                    // Hak bitti: "aradık, resmî video bulunamadı" DÜRÜST sonucu saklanır.
                    outcome = MatchVideoVerificationStatuses.Unavailable;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Beklenmeyen hata da bir arama sonucu değildir: sayılmaz.
                blocked = true;
                outcome = BlockedError;
                _logger.LogWarning(ex, "[POST-MATCH VIDEO] {MatchId} icin arama basarisiz.", m.Id);
            }

            if (blocked)
                repo.RecordFixtureAttemptBlocked(extId, FixtureRefreshPurposes.PostMatchVideo, nowUtc, outcome);
            else
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

        // TAKVİME BAŞLAMIŞ MAÇLAR PENCEREDEN DÜŞMEZ (11.09.2026): ekran "en az bir deneme
        // hakkı varsa kontrol ediliyor" der. Pencere dışına düşen ama 1–3 gerçek denemesi
        // olan bir maç bir daha hiç aranmasaydı bu cümle boş bir vaat olurdu. Üst sınır
        // (StartedScheduleCeiling) sonsuz geriye tarama yapılmasını önler.
        var ceiling = nowUtc - StartedScheduleCeiling;
        var startedIds = await db.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.Purpose == FixtureRefreshPurposes.PostMatchVideo)
            .GroupBy(a => a.ExternalMatchId)
            .Select(g => new { Id = g.Key, Attempts = g.Sum(a => a.AttemptCount) })
            .Where(x => x.Attempts > 0 && x.Attempts < MaxAttempts)
            .Select(x => x.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        if (startedIds.Count > 0)
        {
            var known = new HashSet<int>(rows.Select(r => r.Id));
            var resumed = await db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished
                         && m.MatchDate < since && m.MatchDate >= ceiling
                         && locked.Contains(m.LeagueId)
                         && m.ExternalMatchId != null
                         && startedIds.Contains(m.ExternalMatchId))
                .ToListAsync(ct).ConfigureAwait(false);
            rows.AddRange(resumed.Where(m => known.Add(m.Id)));
        }

        return rows
            .OrderByDescending(m => followedIds.Contains(m.Id))
            .ThenByDescending(m => m.MatchDate)
            .ToList();
    }
}
