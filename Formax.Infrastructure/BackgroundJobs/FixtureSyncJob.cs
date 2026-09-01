using Formax.Application.Services.Standings;
using Formax.Infrastructure.Providers;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs;

/// <summary>
/// Sprint 0 — Fixture sync background job.
///
/// Cadence   : every 6 hours.
/// Window    : yesterday → today + 7 days.
/// Stagger   : 30 s startup delay to avoid boot-time DB hammering.
///
/// Cycle flow:
///   0. Acquire distributed lock — only one instance runs per cycle.
///   1. Fetch all fixtures in window from ISportsDataProvider (1 HTTP request)
///   2. Collect all unique external team IDs from the response
///   3. Batch-load existing teams (1 DB query)
///   4. Upsert teams: add new / update name + logo if changed
///   5. SaveChanges for teams → EF assigns IDs to new rows
///   6. Build externalTeamId → internal Id map
///   7. Batch-load existing matches (1 DB query)
///   8. Upsert matches: add new / update date + status + league if existing
///   9. SaveChanges for matches
///  10. Heartbeat lock so peers know this instance is still alive.
///
/// Total DB round-trips per cycle: 4 (2 reads + 2 writes) + 2 lock ops.
/// Total HTTP requests per cycle : 1.
///
/// Error policy: full cycle failure is caught and logged; job keeps running.
///              Lock is released on graceful shutdown so peers can take over
///              immediately instead of waiting for the staleness timeout.
/// Provider failure returns empty list — no side effects.
/// </summary>
public sealed class FixtureSyncJob : BackgroundService
{
    private static readonly TimeSpan StartupDelay   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LoopDelay      = TimeSpan.FromHours(6);

    /// <summary>
    /// Lock staleness threshold.  If HeartbeatAt is older than this the lock
    /// is considered abandoned and another instance may take it.
    /// Set to 30 minutes — well above the 6-hour cycle; a missed heartbeat
    /// after a crash is detectable within half an hour.
    /// </summary>
    private static readonly TimeSpan LockStaleness  = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly ILogger<FixtureSyncJob>   _logger;

    /// <summary>
    /// Unique id for this process instance: hostname + PID + random guid.
    /// Guarantees uniqueness even when multiple instances run on the same host.
    /// </summary>
    private readonly string _instanceId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public FixtureSyncJob(
        IServiceScopeFactory    scopeFactory,
        ILogger<FixtureSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[FIXTURE SYNC] Job started — instance {InstanceId}.", _instanceId);

        await Task.Delay(StartupDelay, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIXTURE SYNC] Cycle failed — will retry in {Delay}.", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }
        finally
        {
            // Best-effort release on graceful shutdown so peers can take over
            // immediately rather than waiting for the staleness timeout.
            await TryReleaseLockAsync();
        }

        _logger.LogInformation("[FIXTURE SYNC] Job stopped.");
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task TryReleaseLockAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lockRepo = scope.ServiceProvider
                .GetRequiredService<IFixtureSyncLockRepository>();
            await lockRepo.ReleaseAsync(_instanceId);
            _logger.LogInformation(
                "[FIXTURE SYNC] Lock released — instance {InstanceId}.", _instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[FIXTURE SYNC] Could not release lock on shutdown — will expire after {Staleness} min.",
                LockStaleness.TotalMinutes);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GERİ DOLDURMA — mevcut sync döngüsünü GEÇMİŞ bir pencere için çalıştırır.
    /// Yeni ingestion yolu değildir: aynı RunCycleAsync, yalnız gün penceresi dışarıdan
    /// verilir. Şema genişlediğinde (ör. ilk yarı skoru) eski tamamlanmış maçların
    /// güncellenmesi için gerekir. Maliyet: pencere gün sayısı kadar istek.
    /// </summary>
    public async Task RunBackfillAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        // SESSİZ BAŞARI YOK: kilit başka bir örnekte/ölü bir örnekte duruyorsa tur HİÇ çalışmaz.
        // Eskiden bu durum çağırana "tamam" gibi dönüyordu (ölçüldü: 29.08 14:55'te kalan kilit
        // yüzünden backfill 200 dönüp tek satır yazmadı). Artık çağıran bunu ayırt edebiliyor.
        if (!await RunCycleAsync(ct, fromDate, toDate))
            throw new FixtureSyncBusyException(
                "Fikstür senkronizasyon kilidi başka bir örnekte — bu turda hiçbir şey yazılmadı. " +
                $"Kilit en geç {LockStaleness.TotalMinutes:0} dk içinde bayatlar ve yeniden denenebilir.");
    }

    /// <summary>
    /// Bir turda sonuç uzlaştırması için EK olarak sorulacak geçmiş günler.
    ///
    /// Aday ölçütü <see cref="SeasonDataCompleteness"/> ile AYNI paydır (210 dk): puan
    /// durumunun "eksik" saydığı maç ile sonuç zincirinin "aranacak" saydığı maç aynı
    /// kümedir — iki farklı eşik iki farklı gerçek üretmesin.
    ///
    /// BÜTÇE: gün sayısı <c>ApiFootball:ResultReconciliation:MaxDaysPerCycle</c> ile
    /// sınırlıdır (varsayılan 6). En eski günler önce kapatılır; kalanlar bir sonraki
    /// tura ertelenir. Böylece 10 günlük bir birikim tek turda kotayı yakmaz ve her tur
    /// eksiği bir miktar azaltır. İleri penceredeki günler burada TEKRAR sayılmaz.
    /// </summary>
    private ReconciliationPlan BuildReconciliationPlan(
        IFixtureSyncRepository repo,
        IConfiguration config,
        TimeZoneInfo tz,
        IReadOnlyCollection<DateTime> windowDays)
    {
        var plan = new ReconciliationPlan();

        var maxDays = Math.Max(0, config.GetValue("ApiFootball:ResultReconciliation:MaxDaysPerCycle", 6));
        if (maxDays == 0) return plan;

        // Kapsam = okuma tarafıyla AYNI allow-list. Kapsam dışı ligler zaten upsert'te
        // eleniyor; onlar için gün satın almak boşa istek olurdu.
        var leagues = CoveragePolicy.LeagueAllowList(config);

        List<Formax.Domain.Entities.Match> candidates;
        try
        {
            candidates = repo.GetStaleResultCandidates(
                DateTime.UtcNow, SeasonDataCompleteness.SettleMarginMinutes, leagues);
        }
        catch (Exception ex)
        {
            // Aday sorgusu düşerse normal fikstür senkronu AYNEN devam eder.
            _logger.LogWarning(ex, "[FIXTURE SYNC] Sonuç uzlaştırma adayları okunamadı — yalnız ileri pencere sorulacak.");
            return plan;
        }

        plan.CandidateCount = candidates.Count;
        if (candidates.Count == 0) return plan;

        // Sağlayıcı günü YEREL tz'ye göre gruplar (istekler timezone= ile gidiyor);
        // MatchDate UTC olduğu için aynı tz'ye çevrilir, yoksa gece maçları yanlış güne düşer.
        var known = new HashSet<DateTime>(windowDays);
        var days = candidates
            .Select(m => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc), tz).Date)
            .Distinct()
            .Where(d => !known.Contains(d))
            .OrderBy(d => d)
            .ToList();

        plan.Days.AddRange(days.Take(maxDays));
        plan.DeferredDayCount = Math.Max(0, days.Count - plan.Days.Count);
        return plan;
    }

    /// <summary>
    /// Gün-bazlı çekimin ABONELİK PLANI tarafından kapatıldığı maçların sonucunu tek tek
    /// <c>fixtures?id=</c> ile alır. Yalnız şu maçlar için çalışır:
    ///   • sonuç uzlaştırma adayıdır (kickoff + 210 dk geçmiş, kesin sonucu yok), VE
    ///   • gün-bazlı çekim o günü PLAN nedeniyle getiremedi, VE
    ///   • bu turda toplu yanıtın içinde zaten gelmedi.
    ///
    /// Tavan: <c>ApiFootball:ResultReconciliation:MaxFixtureLookupsPerCycle</c> (varsayılan 10).
    /// En eski maç önce kapatılır; kalanlar sonraki tura ertelenir. Sağlayıcı sonucu
    /// dönmezse HİÇBİR ŞEY yazılmaz — 0-0 uydurulmaz.
    ///
    /// Plan yükseltilip gün-bazlı çekim geçmişi kapsadığında bu adım kendiliğinden
    /// 0 istek üretir (plan-kapalı gün kalmaz).
    /// </summary>
    private async Task<List<Formax.Application.DTOs.Fixtures.SportsFixtureResult>> RecoverPlanBlockedResultsAsync(
        ISportsDataProvider provider,
        IFixtureSyncRepository repo,
        IConfiguration config,
        TimeZoneInfo tz,
        Formax.Application.DTOs.Fixtures.SportsFixtureDayBatch batch,
        ReconciliationPlan reconciliation,
        CancellationToken ct)
    {
        var recovered = new List<Formax.Application.DTOs.Fixtures.SportsFixtureResult>();

        var maxLookups = Math.Max(0, config.GetValue("ApiFootball:ResultReconciliation:MaxFixtureLookupsPerCycle", 10));
        if (maxLookups == 0 || batch.PlanBlockedDates.Count == 0 || reconciliation.CandidateCount == 0)
            return recovered;

        var blocked = new HashSet<DateTime>(batch.PlanBlockedDates);
        var alreadyFetched = new HashSet<string>(
            batch.Fixtures.Select(f => f.ExternalMatchId ?? string.Empty), StringComparer.Ordinal);

        List<Formax.Domain.Entities.Match> candidates;
        try
        {
            candidates = repo.GetStaleResultCandidates(
                DateTime.UtcNow,
                SeasonDataCompleteness.SettleMarginMinutes,
                CoveragePolicy.LeagueAllowList(config));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FIXTURE SYNC] Tekil sonuç çekimi için adaylar okunamadı.");
            return recovered;
        }

        // ÖNCELİK: sezon metadata'sı DOĞRULANMIŞ ligler önce. Sınırlı bütçede harcanan her
        // istek, gerçekten yayımlanan bir puan durumunun "eksik" damgasını kaldırmalı;
        // sezonu çözülemeyen bir ligin sonucu hiçbir tabloyu tamamlamaz. Aynı öncelik
        // içinde en eski maç önce kapatılır.
        HashSet<int> verifiedLeagues;
        try { verifiedLeagues = repo.GetLeaguesWithVerifiedSeason(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FIXTURE SYNC] Sezon metadata ligleri okunamadı — sıralama yalnız tarihe göre.");
            verifiedLeagues = new HashSet<int>();
        }

        var targets = candidates
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
            .Where(m => !alreadyFetched.Contains(m.ExternalMatchId!))
            .Where(m => blocked.Contains(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc), tz).Date))
            .OrderByDescending(m => verifiedLeagues.Contains(m.LeagueId))
            .ThenBy(m => m.MatchDate)
            .Take(maxLookups)
            .ToList();

        if (targets.Count == 0) return recovered;

        _logger.LogInformation(
            "[FIXTURE SYNC] Plan-kapalı günler için tekil sonuç çekimi — {Count} maç (tavan {Max}).",
            targets.Count, maxLookups);

        // DAKİKA LİMİTİ: ölçüldü 31.08.2026 — Free plan 10 istek/dakika. Tekil çekimler
        // arka arkaya yapıldığında son 3'ü "Too many requests" ile geri döndü; bu istekler
        // kotadan DÜŞÜYOR ama hiçbir sonuç getirmiyordu. Aralarına boşluk koymak, aynı
        // kotayla daha çok sonuç almak demektir.
        var spacingMs = Math.Max(0, config.GetValue("ApiFootball:ResultReconciliation:FixtureLookupSpacingMs", 7000));
        var first = true;

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();

            if (!first && spacingMs > 0) await Task.Delay(spacingMs, ct);
            first = false;

            var result = await provider.GetFixtureByIdAsync(target.ExternalMatchId!, ct);
            if (result == null)
            {
                _logger.LogWarning(
                    "[FIXTURE SYNC] {MatchId} (fixture {ExtId}) — sağlayıcı sonuç vermedi, HİÇBİR ŞEY yazılmadı.",
                    target.Id, target.ExternalMatchId);
                continue;
            }
            recovered.Add(result);
        }

        return recovered;
    }

    /// <summary>
    /// Sonucun geldiği GERÇEK sağlayıcı sorgusu. Sorgu gün-bazlı olduğu için etiket de
    /// gün-bazlıdır; uydurulmuş bir kaynak adı yazılmaz.
    /// </summary>
    private static string ResultSourceTag(
        Formax.Application.DTOs.Fixtures.SportsFixtureResult fixture,
        TimeZoneInfo tz,
        IReadOnlySet<string> perFixtureIds)
    {
        // Tekil kurtarma ile gelen sonucun kaynağı gün sorgusu DEĞİLDİR; olduğu gibi yazılır.
        if (fixture.ExternalMatchId != null && perFixtureIds.Contains(fixture.ExternalMatchId))
            return $"api-football:fixtures?id={fixture.ExternalMatchId}";

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(fixture.MatchDate, DateTimeKind.Utc), tz).Date;
        return $"api-football:fixtures?date={local:yyyy-MM-dd}";
    }

    private sealed class ReconciliationPlan
    {
        public List<DateTime> Days { get; } = new();
        public int CandidateCount { get; set; }
        public int DeferredDayCount { get; set; }
    }

    /// <returns>Tur gerçekten çalıştıysa true; kilit alınamadığı için hiç çalışmadıysa false.</returns>
    private async Task<bool> RunCycleAsync(CancellationToken ct, DateTime? fromOverride = null, DateTime? toOverride = null)
    {
        // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
        using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(FixtureSyncJob));

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var lockRepo  = sp.GetRequiredService<IFixtureSyncLockRepository>();
        var provider  = sp.GetRequiredService<ISportsDataProvider>();
        var repo      = sp.GetRequiredService<IFixtureSyncRepository>();
        var statsRepo = sp.GetRequiredService<IMatchLiveStatsRepository>();
        var config    = sp.GetRequiredService<IConfiguration>();

        // ── 0. Distributed lock ───────────────────────────────────────────────
        if (!await lockRepo.TryAcquireAsync(_instanceId, LockStaleness, ct))
        {
            _logger.LogDebug(
                "[FIXTURE SYNC] Lock held by another instance — skipping cycle.");
            return false;
        }

        // MVP Release Hardening: gün penceresi KONFİGÜRE tz'ye göre (UTC-origin gün kaymasını önler).
        // provider aynı tz'yi timezone= ile gönderir → "dün" yerel güne göre tutarlı olur.
        var tzId = ApiFootballTimeZone.ResolveId(config[ApiFootballTimeZone.ConfigKey]);
        var tz   = ApiFootballTimeZone.TryResolve(tzId);
        if (tz == null)
        {
            _logger.LogWarning(
                "[FIXTURE SYNC] Timezone '{Tz}' çözülemedi — UTC'ye düşülüyor (gün kayması olabilir).", tzId);
            tz = TimeZoneInfo.Utc;
        }

        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        // Override verilmişse geri doldurma penceresi kullanılır (admin tetiği).
        var fromDate = fromOverride?.Date ?? todayLocal.AddDays(-1);   // yesterday — status reconciliation
        var toDate   = toOverride?.Date   ?? todayLocal.AddDays(7);    // today + 7

        // ── 0b. GÜN KÜMESİ = ileri pencere + GEÇMİŞTEKİ EKSİK SONUÇ GÜNLERİ ───
        // KÖK NEDEN (ölçüldü 31.08.2026): pencere "dün → bugün+7" olduğu için başlama saati
        // 1 günden fazla geçmiş bir maç bir daha HİÇ sorulmuyordu. O maç hangi sebeple
        // (kota, kilit, çöken tur) kaçırıldıysa sonsuza dek NotStarted/Live kalıyordu —
        // La Liga'da 11, Espanyol–Real Madrid (22.08) 9 gündür "Live".
        // Çözüm sonuç için AYRI bir sistem değildir: aynı turun GÜN KÜMESİNE eksik sonuçların
        // günleri eklenir. İstek maç başına değil GÜN başına üretilir; bir günün tek yanıtı
        // o güne düşen bütün eksik maçları kapatır.
        var windowDays = new List<DateTime>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1)) windowDays.Add(d);

        var reconciliation = BuildReconciliationPlan(repo, config, tz, windowDays);
        var requestDays = windowDays.Concat(reconciliation.Days).Distinct().OrderBy(d => d).ToList();

        if (reconciliation.Days.Count > 0)
            _logger.LogInformation(
                "[FIXTURE SYNC] Sonuç uzlaştırma — {Candidates} aday maç, {Days} ek gün " +
                "({Skipped} gün bu turda bütçe tavanı nedeniyle ertelendi).",
                reconciliation.CandidateCount, reconciliation.Days.Count, reconciliation.DeferredDayCount);

        // ── 1. Fetch — gün başına TEK istek, bir günün hatası diğerlerini düşürmez ──
        var metrics = sp.GetRequiredService<Formax.Infrastructure.Telemetry.ApiFootballMetrics>();
        var meterBefore = metrics.Snapshot();

        var batch = await provider.GetFixturesForDatesAsync(requestDays, ct);

        var meterAfter   = metrics.Snapshot();
        var realRequests = meterAfter.TotalRequests - meterBefore.TotalRequests;
        var l1Hits       = meterAfter.CacheHits - meterBefore.CacheHits;
        var l2Hits       = meterAfter.PersistentCacheHits - meterBefore.PersistentCacheHits;

        _logger.LogInformation(
            "[FIXTURE SYNC] Sağlayıcı — {Days} gün istendi, {Ok} alındı, {Fail} erişilemedi; " +
            "GERÇEK istek {Real}, L1 hit {L1}, L2 hit {L2}.",
            batch.RequestedDayCount, batch.SucceededDates.Count, batch.FailedDates.Count,
            realRequests, l1Hits, l2Hits);

        var planBlocked = new HashSet<DateTime>(batch.PlanBlockedDates);
        foreach (var (day, reason) in batch.FailedDates)
            _logger.LogWarning(
                planBlocked.Contains(day)
                    ? "[FIXTURE SYNC] {Day} — {Reason}. Bu gün TEKRAR DENENMEZ; sonuçlar fikstür kimliğiyle alınacak."
                    : "[FIXTURE SYNC] {Day} alınamadı — {Reason}. Bu gün SONRAKİ turda yeniden denenir.",
                day.ToString("yyyy-MM-dd"), reason);

        // ── 1a. PLAN-KAPALI GÜNLER İÇİN TEKİL SONUÇ ÇEKİMİ ───────────────────
        // Toplu gün çekimi HÂLÂ birincil yoldur. Ama abonelik planı geçmiş günleri
        // date= sorgusuna kapattığında (ölçüldü 31.08.2026: Free plan yalnız
        // [bugün-1, bugün+1]; ids= toplu parametresi de kapalı) o maçların sonucunu
        // almanın BAŞKA yolu yoktur. Bu, ikinci bir sonuç sistemi değildir: aynı turda,
        // aynı upsert yoluna beslenen bir GERİ ÇEKİLME adımıdır ve tur başına sert
        // tavanı vardır — plan açık olsaydı hiç çalışmazdı.
        var recovered = await RecoverPlanBlockedResultsAsync(
            provider, repo, config, tz, batch, reconciliation, ct);
        var perFixtureSourceIds = new HashSet<string>(
            recovered.Select(r => r.ExternalMatchId ?? string.Empty), StringComparer.Ordinal);
        if (recovered.Count > 0)
        {
            batch.Fixtures.AddRange(recovered);
            var meterFallback = metrics.Snapshot();
            _logger.LogInformation(
                "[FIXTURE SYNC] Tekil sonuç çekimi — {Count} fikstür alındı; " +
                "turun TOPLAM gerçek isteği {Real}.",
                recovered.Count, meterFallback.TotalRequests - meterBefore.TotalRequests);
        }

        // HİÇBİR gün alınamadıysa VE tekil çekim de bir şey getirmediyse tur BAŞARISIZDIR.
        if (batch.Fixtures.Count == 0 && batch.IsTotalFailure)
            throw new ApiFootballSportsDataProvider.ApiFootballUnavailableException(
                $"fixtures — {batch.RequestedDayCount} günün hiçbiri alınamadı: " +
                string.Join("; ", batch.FailedDates.Select(f => $"{f.Date:yyyy-MM-dd}: {f.Reason}")));

        var fixtures = batch.Fixtures;

        if (fixtures.Count == 0)
        {
            _logger.LogDebug("[FIXTURE SYNC] No fixtures returned for window {From}→{To}.", fromDate, toDate);
            await lockRepo.HeartbeatAsync(_instanceId, ct);
            return true;   // tur çalıştı: sağlayıcı bu pencerede gerçekten 0 fikstür verdi
        }

        // ── 1b. GDP Final Evolution — COVERAGE-DRIVEN Discovery (elle allow-list yerine) ──
        // FixtureDiscovery artık sabit liste değil, ligin ÖĞRENİLMİŞ coverage tier'ına göre çalışır.
        // DiscoveryFloorTier (config) taban tier; varsayılan "Passive" = kısıtlama yok (geri-uyum, cold-start
        // liglerin öğrenme şansı korunur). Yükseltilirse yalnız o tier'ın üstündeki ligler Matches'e girer.
        var floorTier = CoveragePolicy.DiscoveryFloorTier(config);
        var floorRank = CoveragePolicy.TierRank(floorTier);
        if (floorRank > 0)
        {
            var coverage = sp.GetRequiredService<ILeagueCoverageService>();
            var tierByLeague = coverage.ComputeAll().ToDictionary(l => l.LeagueId, l => l.Tier);
            var before = fixtures.Count;
            fixtures = fixtures.Where(f =>
            {
                var tier = tierByLeague.TryGetValue(f.LeagueExternalId, out var t) ? t : "Passive";
                return CoveragePolicy.TierRank(tier) >= floorRank;
            }).ToList();
            _logger.LogInformation(
                "[FIXTURE SYNC] Coverage-driven discovery (taban {Tier}) — {Kept}/{Before} fixture tutuldu.",
                floorTier, fixtures.Count, before);
            if (fixtures.Count == 0) return true;
        }

        // Opsiyonel SERT override — açık lig allow-list'i (config) doluysa ek kısıtlama.
        var allow = CoveragePolicy.LeagueAllowList(config);
        if (allow.Count > 0)
            fixtures = fixtures.Where(f => CoveragePolicy.Allows(allow, f.LeagueExternalId)).ToList();

        _logger.LogInformation(
            "[FIXTURE SYNC] Fetched {Count} fixture(s) for {From}→{To}.",
            fixtures.Count, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));

        // ── 2. Collect unique external team IDs ──────────────────────────────
        var allExternalTeamIds = fixtures
            .SelectMany(f => new[] { f.HomeTeamExternalId, f.AwayTeamExternalId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        // ── 3. Batch-load existing teams (1 query) ───────────────────────────
        var existingTeams = repo.GetTeamsByExternalIds(allExternalTeamIds);

        int teamsAdded   = 0;
        int teamsUpdated = 0;

        // ── 4. Upsert teams ──────────────────────────────────────────────────
        foreach (var externalId in allExternalTeamIds)
        {
            // Determine name + logo from first fixture that references this team
            var refFixture = fixtures.FirstOrDefault(f =>
                f.HomeTeamExternalId == externalId || f.AwayTeamExternalId == externalId);
            if (refFixture == null) continue;

            var name    = refFixture.HomeTeamExternalId == externalId
                          ? refFixture.HomeTeamName
                          : refFixture.AwayTeamName;
            var logoUrl = refFixture.HomeTeamExternalId == externalId
                          ? refFixture.HomeLogoUrl
                          : refFixture.AwayLogoUrl;

            if (existingTeams.TryGetValue(externalId, out var existing))
            {
                // Update name / logo if changed — avoids unnecessary dirty writes
                if (existing.Name != name || existing.LogoUrl != logoUrl)
                {
                    existing.Name    = name;
                    existing.LogoUrl = logoUrl;
                    teamsUpdated++;
                }
            }
            else
            {
                repo.AddTeam(new Team
                {
                    Name           = name,
                    ExternalTeamId = externalId,
                    LogoUrl        = logoUrl,
                    CreatedAt      = DateTime.UtcNow
                });
                teamsAdded++;
            }
        }

        // ── 5. Save teams (new rows get DB-generated IDs) ────────────────────
        await repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[FIXTURE SYNC] Teams — added {Added}, updated {Updated}.",
            teamsAdded, teamsUpdated);

        // ── 6. Rebuild team ID map (covers newly inserted rows) ──────────────
        var teamMap = repo.GetTeamsByExternalIds(allExternalTeamIds);

        // ── 7. Batch-load existing matches (1 query) ─────────────────────────
        var allExternalMatchIds = fixtures
            .Select(f => f.ExternalMatchId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        var existingMatches = repo.GetMatchesByExternalIds(allExternalMatchIds);

        int matchesAdded   = 0;
        int matchesUpdated = 0;
        int matchesSkipped = 0;

        // Collected finished matches whose score must be mirrored to MatchLiveStats
        // after SaveChanges (so new rows have DB-assigned IDs).
        var finishedScoreMatches = new List<(Match match, int home, int away)>();

        // ── 8. Upsert matches ────────────────────────────────────────────────
        foreach (var fixture in fixtures)
        {
            if (string.IsNullOrWhiteSpace(fixture.ExternalMatchId)) continue;

            // Resolve internal team IDs — skip if either team cannot be resolved
            if (!teamMap.TryGetValue(fixture.HomeTeamExternalId, out var homeTeam) ||
                !teamMap.TryGetValue(fixture.AwayTeamExternalId, out var awayTeam))
            {
                _logger.LogWarning(
                    "[FIXTURE SYNC] Skipping fixture {FixtureId} — unresolved team(s) " +
                    "home={HomeExtId} away={AwayExtId}.",
                    fixture.ExternalMatchId,
                    fixture.HomeTeamExternalId,
                    fixture.AwayTeamExternalId);
                matchesSkipped++;
                continue;
            }

            // Finished fixtures carry a real final score from the provider.
            // Live scores are owned exclusively by the live engine (Locked
            // Decision #6) — FixtureSync never writes them. NotStarted = 0.
            var isFinished = fixture.Status == "Finished"
                             && fixture.HomeScore.HasValue
                             && fixture.AwayScore.HasValue;

            if (existingMatches.TryGetValue(fixture.ExternalMatchId, out var existingMatch))
            {
                // Update mutable fields only.
                existingMatch.MatchDate   = fixture.MatchDate;
                // KESİN SONUÇ GERİ ALINMAZ: sağlayıcı tanımadığımız bir status kodu
                // döndürdüğünde eşleyici "NotStarted"a düşer; bu, bitmiş bir maçı yeniden
                // "başlamamış" yapıp sonucu SİLERDİ. Finished'tan çıkış yalnız sağlayıcı
                // maçın oynanmadığını AÇIKÇA söylediğinde (iptal/ertelenme) kabul edilir.
                var isResultRegression =
                    string.Equals(existingMatch.Status, "Finished", StringComparison.OrdinalIgnoreCase) &&
                    fixture.Status is not ("Finished" or "Cancelled" or "Postponed");
                // SKORSUZ "BİTTİ" KABUL EDİLMEZ: sağlayıcı "Finished" deyip skor vermediğinde
                // elimizde KESİN SONUÇ YOKTUR. Durumu yine de Finished yazmak, maçı 0-0
                // oynanmış gibi gösterir ve o uydurma skor puan durumuna girerdi. Böyle bir
                // yanıt "veri henüz gelmedi" demektir; maç bir sonraki turda yeniden sorulur.
                var finishedWithoutScore =
                    string.Equals(fixture.Status, "Finished", StringComparison.OrdinalIgnoreCase) && !isFinished;

                if (isResultRegression)
                {
                    _logger.LogWarning(
                        "[FIXTURE SYNC] {FixtureId} — sağlayıcı '{New}' dedi ama depoda Finished; " +
                        "sonuç KORUNDU (geri alma reddedildi).",
                        fixture.ExternalMatchId, fixture.Status);
                }
                else if (finishedWithoutScore)
                {
                    _logger.LogWarning(
                        "[FIXTURE SYNC] {FixtureId} — sağlayıcı 'Finished' dedi ama SKOR VERMEDİ; " +
                        "durum YAZILMADI (0-0 uydurulmaz), maç sonraki turda yeniden sorulacak.",
                        fixture.ExternalMatchId);
                }
                else
                {
                    existingMatch.Status  = fixture.Status;
                }
                existingMatch.League      = fixture.LeagueName;
                existingMatch.LeagueId    = fixture.LeagueExternalId;
                existingMatch.HomeTeamId  = homeTeam.Id;
                existingMatch.AwayTeamId  = awayTeam.Id;
                // Update referee/venue if provider supplies them (never clear existing)
                if (!string.IsNullOrWhiteSpace(fixture.Referee))  existingMatch.Referee = fixture.Referee;
                if (!string.IsNullOrWhiteSpace(fixture.Venue))    existingMatch.Venue   = fixture.Venue;
                // Tur/aşama: sağlayıcı verdiyse yazılır (şema sonrası geri doldurma da buradan).
                if (!string.IsNullOrWhiteSpace(fixture.Round))    existingMatch.Round   = fixture.Round;
                // Finished ONLY — never touch Live/NotStarted scores.
                if (isFinished)
                {
                    existingMatch.HomeScore = fixture.HomeScore!.Value;
                    existingMatch.AwayScore = fixture.AwayScore!.Value;
                    // İlk yarı — sağlayıcı verdiyse yazılır, vermediyse MEVCUT değer korunur
                    // (null ile üzerine yazıp veri kaybetmeyelim).
                    if (fixture.HalfTimeHomeScore.HasValue && fixture.HalfTimeAwayScore.HasValue)
                    {
                        existingMatch.HalfTimeHomeScore = fixture.HalfTimeHomeScore;
                        existingMatch.HalfTimeAwayScore = fixture.HalfTimeAwayScore;
                    }
                    // Sonuç tazeliği/kaynağı — "bu skoru ne zaman ve neyden aldık" izi.
                    // Yalnız GERÇEK sağlayıcı sonucu yazıldığında damgalanır.
                    existingMatch.ResultUpdatedAtUtc = DateTime.UtcNow;
                    existingMatch.ResultSource       = ResultSourceTag(fixture, tz, perFixtureSourceIds);
                    finishedScoreMatches.Add((existingMatch, fixture.HomeScore.Value, fixture.AwayScore.Value));
                }
                matchesUpdated++;
            }
            else
            {
                var newMatch = new Match
                {
                    ExternalMatchId = fixture.ExternalMatchId,
                    MatchDate       = fixture.MatchDate,
                    // Skorsuz "Finished" yeni kayıtta da kabul edilmez (bkz. güncelleme dalı):
                    // sonucu olmayan maç "bitmiş" diye kaydedilirse 0-0 uydurma bir sonuca döner.
                    Status          = string.Equals(fixture.Status, "Finished", StringComparison.OrdinalIgnoreCase) && !isFinished
                                      ? "NotStarted"
                                      : fixture.Status,
                    League          = fixture.LeagueName,
                    LeagueId        = fixture.LeagueExternalId,
                    HomeTeamId      = homeTeam.Id,
                    AwayTeamId      = awayTeam.Id,
                    HomeScore       = isFinished ? fixture.HomeScore!.Value : 0,
                    AwayScore       = isFinished ? fixture.AwayScore!.Value : 0,
                    // Oynanmamış maçta İY null kalır (0 yazılmaz — 0 gerçek skordur).
                    HalfTimeHomeScore = isFinished ? fixture.HalfTimeHomeScore : null,
                    HalfTimeAwayScore = isFinished ? fixture.HalfTimeAwayScore : null,
                    Referee         = fixture.Referee,
                    Venue           = fixture.Venue,
                    // Sağlayıcının gerçek tur/aşama adı — maç türünün TEK kaynağı.
                    Round           = string.IsNullOrWhiteSpace(fixture.Round) ? null : fixture.Round,
                    CreatedAt       = DateTime.UtcNow,
                    // Oynanmamış maçta sonuç damgası YOK (null) — 0-0 bir sonuç değildir.
                    ResultUpdatedAtUtc = isFinished ? DateTime.UtcNow : null,
                    ResultSource       = isFinished ? ResultSourceTag(fixture, tz, perFixtureSourceIds) : null
                };
                repo.AddMatch(newMatch);
                if (isFinished)
                    finishedScoreMatches.Add((newMatch, fixture.HomeScore!.Value, fixture.AwayScore!.Value));
                matchesAdded++;
            }
        }

        // ── 9. Save matches (new rows get DB-generated IDs) ──────────────────
        await repo.SaveChangesAsync(ct);

        // ── 9b. Dual-score sync — mirror finished scores into MatchLiveStats ──
        // MatchHeader/Verdict read score from MatchLiveStats; keep both aligned
        // (same pattern as the seed). Live matches are excluded above, so this
        // never races the live ingestion job.
        foreach (var (match, hs, aScore) in finishedScoreMatches)
        {
            var existingStats = statsRepo.GetByMatchId(match.Id);
            await statsRepo.UpsertAsync(new MatchLiveStats
            {
                MatchId   = match.Id,
                HomeScore = hs,
                AwayScore = aScore,
                Minute    = 90,
                Phase     = "FT",
                UpdatedAt = DateTime.UtcNow
            }, existingStats, ct);
        }
        if (finishedScoreMatches.Count > 0)
            await statsRepo.SaveChangesAsync(ct);

        // ── 9b. SONUÇ TETİKLEMELİ PUAN DURUMU YENİLEME ───────────────────────
        // Sonuç DB ye KESİN olarak yazıldıktan SONRA (yukarıdaki SaveChanges) ilgili
        // lig+sezon puan durumu saatlik turu beklemeden yeniden hesaplanır. Settlement
        // zinciri değişmez; burada yalnız kendi verimizden projeksiyon üretilir ve
        // hiçbir sağlayıcıya istek gitmez.
        if (finishedScoreMatches.Count > 0)
        {
            var standings = sp.GetRequiredService<Formax.Application.Interfaces.ILeagueStandingsService>();
            var touched = finishedScoreMatches
                .Select(x => (x.match.LeagueId, x.match.MatchDate))
                .GroupBy(x => x.LeagueId)
                .Select(g => (LeagueId: g.Key, MatchDate: g.Max(x => x.MatchDate)))
                .ToList();

            foreach (var (leagueId, matchDate) in touched)
            {
                try
                {
                    await standings.RefreshForSettledMatchAsync(leagueId, matchDate, ct);
                }
                catch (Exception ex)
                {
                    // Puan durumu projeksiyonu fikstür senkronunu ASLA düşürmez.
                    _logger.LogWarning(ex,
                        "[FIXTURE SYNC] Standings projection refresh failed for league {LeagueId}.",
                        leagueId);
                }
            }
        }

        _logger.LogInformation(
            "[FIXTURE SYNC] Matches — added {Added}, updated {Updated}, skipped {Skipped}.",
            matchesAdded, matchesUpdated, matchesSkipped);

        // ── 10. Heartbeat ────────────────────────────────────────────────────
        await lockRepo.HeartbeatAsync(_instanceId, ct);
        return true;
    }
}

/// <summary>
/// Geri doldurma turu kilit yüzünden HİÇ çalışmadı. Çağıran bunu "başarılı ama 0 fikstür"
/// ile karıştırmasın diye ayrı tiptir; yazma yapılmadığı garantidir.
/// </summary>
public sealed class FixtureSyncBusyException : Exception
{
    public FixtureSyncBusyException(string message) : base(message) { }
}
