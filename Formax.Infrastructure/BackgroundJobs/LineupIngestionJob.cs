using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// RESMÎ KADRO JOB'I — dakikada bir tur; dış istek yalnız slotlarda.
    ///
    /// ÜRÜN KARARI: kadro API-Football'dan ALINMAZ (<c>fixtures/lineups</c> isteği = 0).
    /// Kadro lig/federasyon/kulübün resmî yayınından <see cref="OfficialSources.OfficialLineupCollector"/>
    /// ile toplanır. SLOTLAR (<see cref="Formax.Application.Services.OfficialSources.OfficialLineupSchedule"/>):
    /// T−60, T−45, T−30, T−20, T−15, T−10, T−5 ve kickoff+10 son kontrol.
    ///
    /// Kadro bulunduğunda (iki taraf doğrulandı) o maç için arama durur; boş cevap başarı
    /// sayılmaz ve sonraki slotu engellemez. Kadro ilk kez DB'ye yazıldığında aktif
    /// takipçilere tek bildirim (MATCH_LINEUP_AVAILABLE) gider — collector'dadır.
    ///
    /// Sakatlık/ceza listesi (kadro DEĞİL) ayrı ufukla bu job'da kalır.
    /// </summary>
    public sealed class LineupIngestionJob : BackgroundService
    {
        /// <summary>
        /// Tur aralığı. T−15/T−10/T−5 slotları 5 dakika arayla olduğu için tur 1 dakikadır;
        /// dış istek slot kuralıyla sınırlıdır, sık tur yalnız DB okumasıdır.
        /// </summary>
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Sakatlık/ceza taramasının ufku. 72 saat iken 3 günlük her fikstür sürekli yoklanıyordu;
        /// oysa sakatlık bilgisi maça 1 günden fazla varken kararı değiştirmiyor. 24 saat, kadro
        /// ufkundan bağımsız kalmaya devam eder ve talebi kaynağında ~3 kat düşürür.
        /// </summary>
        private static readonly TimeSpan StatusLookAhead = TimeSpan.FromHours(12);

        /// <summary>Tur başına sakatlık sorgusu üst sınırı (kota koruması; /injuries 12 saat cache'li).</summary>
        private const int MaxStatusMatchesPerCycle = 4;

        /// <summary>
        /// Bir fikstür için GÜNLÜK sağlayıcı denemesi tavanı — SAKATLIK/CEZA yolu için.
        /// Kadro yolu artık kalıcı deftere bağlıdır ve bu sayacı KULLANMAZ.
        /// </summary>
        private const int MaxProviderAttemptsPerMatchPerDay = 2;

        // matchId → (gün, o gün yapılan deneme sayısı). YALNIZ sakatlık/ceza taraması
        // için; kadro yolunun freni artık FixtureRefreshAttempts tablosudur (kalıcı,
        // restart-safe). Süreç belleğindeki sayaç her açılışta sıfırlandığı için kadro
        // gibi pahalı bir uçta tek başına yeterli DEĞİLDİ.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (DateTime Day, int Count)>
            _attempts = new();

        private static bool AttemptAllowed(int matchId, DateTime utcNow)
        {
            var today = utcNow.Date;
            var entry = _attempts.AddOrUpdate(matchId,
                _ => (today, 1),
                (_, cur) => cur.Day == today ? (today, cur.Count + 1) : (today, 1));
            return entry.Count <= MaxProviderAttemptsPerMatchPerDay;
        }

        private readonly ILogger<LineupIngestionJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        public LineupIngestionJob(
            ILogger<LineupIngestionJob> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[LINEUP JOB] started");

            // Stagger startup to avoid hammering the DB during boot
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LINEUP JOB] unhandled error in RunOnce");
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(LineupIngestionJob));

            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
            var statusRepo = sp.GetRequiredService<IMatchPlayerStatusRepository>();
            var sportsProvider = sp.GetRequiredService<ISportsDataProvider>();

            var utcNow = DateTime.UtcNow;

            // ── KADRO: YALNIZ RESMÎ KAYNAK ────────────────────────────────────────
            // Pencere, slot kararı, kaynak başına tek maç listesi, doğrulama, yazım ve takipçi
            // bildirimi collector'dadır. Burada API-Football kadro isteği ÜRETİLMEZ.
            if (_config.GetValue("OfficialSources:Lineups:Enabled", true))
            {
                var report = await sp.GetRequiredService<OfficialSources.OfficialLineupCollector>()
                    .RunRoundAsync(utcNow, ct);
                if (report.DueMatches == 0)
                    _logger.LogDebug("[LINEUP JOB] {Window} maç penceredeydi, zamanı gelen slot yok.", report.WindowMatches);
            }

            // ── SAKATLIK/CEZA: KADRODAN AYRI, DAHA GENİŞ UFUK ─────────────────────
            // Ölçüldü (14.08): sağlayıcı /injuries?fixture= verisini maç öncesi GÜNLER
            // ÖNCESİNDEN veriyor (Charlton–Derby 18 kayıt), ama bu iş yalnız kickoff'a 60 dk
            // kalan maçlara baktığı için MatchPlayerStatuses yaklaşan maçlarda boş kalıyordu.
            // KADRO (lineups) ufku BİLEREK genişletilmedi — ilk 11 zaten maçtan ~1 saat önce
            // açıklanır ve o uç kotanın en pahalı kalemidir. Yalnız sakatlık/ceza taranır.
            await RefreshPlayerStatusesAsync(matchRepo, statusRepo, sportsProvider, utcNow, ct);
        }

        /// <summary>
        /// TEK MAÇ için RESMÎ kadro doğrulaması — teşhis/geri-doldurma tetiği (admin ucu).
        /// Takvim dışında çalışır ama AYNI okuma/doğrulama/yazma yolunu kullanır; resmî kaynak
        /// kadroyu vermiyorsa hiçbir şey yazılmaz. API-Football'a çıkmaz.
        /// </summary>
        public async Task<OfficialSources.LineupMatchOutcome> RunForMatchAsync(int matchId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OfficialSources.OfficialLineupCollector>()
                .CollectForMatchAsync(matchId, DateTime.UtcNow, ct);
        }

        /// <summary>Sakatlık/ceza taraması — kadro yoklamasından bağımsız, sınırlı bütçeyle.</summary>
        private async Task RefreshPlayerStatusesAsync(
            IMatchReadRepository matchRepo,
            IMatchPlayerStatusRepository statusRepo,
            ISportsDataProvider sportsProvider,
            DateTime utcNow,
            CancellationToken ct)
        {
            // KAPSAM + GÜNLÜK DENEME TAVANI sakatlık yolunda da geçerlidir: kapsam dışı maç için
            // injuries isteği = 0, aynı fikstür gün içinde tavanı aşarsa yeniden sorulmaz.
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var candidates = matchRepo
                .GetUpcomingMatches(utcNow, utcNow + StatusLookAhead)
                .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
                .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                .OrderBy(m => m.MatchDate)
                .Where(m => AttemptAllowed(m.Id, utcNow))
                .Take(MaxStatusMatchesPerCycle)
                .ToList();

            if (candidates.Count == 0) return;

            var touched = 0;
            foreach (var match in candidates)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var statuses = await sportsProvider.GetPlayerStatusesAsync(match.ExternalMatchId!, ct);
                    if (statuses.Count == 0) continue;

                    var entities = statuses.Select(s => new MatchPlayerStatus
                    {
                        Id = Guid.NewGuid(),
                        MatchId = match.Id,
                        TeamId = s.TeamId,
                        PlayerName = s.PlayerName,
                        Status = s.Status,
                        Reason = s.Reason,
                        FetchedAt = utcNow
                    }).ToList();

                    await statusRepo.ReplaceAsync(match.Id, entities, ct);
                    await statusRepo.SaveChangesAsync(ct);
                    touched++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LINEUP JOB] status refresh failed for match {MatchId}", match.Id);
                }
            }

            _logger.LogInformation(
                "[LINEUP JOB] pre-match status refresh — {Touched}/{Total} maç güncellendi.",
                touched, candidates.Count);
        }
    }
}
