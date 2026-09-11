using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Application.UseCases.Follow;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// RESMÎ KADRO JOB'I — dakikada bir tur; sağlayıcı isteği yalnız slotlarda.
    ///
    /// SLOTLAR (<see cref="LineupPollSchedule"/>): T−90, T−60, T−30, T−15, T−10, T−5.
    /// Zamanı gelmiş en yakın denenmemiş slot çalışır; kaçırılan son slot kickoff+10'a
    /// kadar yakalanır. Slot kararı, kalıcı defter ve DB yazımı
    /// <see cref="Lineups.LineupIngestionService"/>'tedir.
    ///
    /// Kadro bir kez açıklandıysa (DB'de her iki taraf da released) o maç için provider'a
    /// hiç gidilmez. Boş cevap negatif cache'lenmez (sonraki slotu engellemesin diye).
    ///
    /// On lineup release:
    ///   1. Persists MatchLineup + MatchLineupPlayers
    ///   2. Persists MatchPlayerStatuses
    ///   3. Fan-outs UserNotification to every follower of that match
    ///   4. Calls INotificationService (webhook / future push)
    /// </summary>
    public sealed class LineupIngestionJob : BackgroundService
    {
        /// <summary>
        /// Tur aralığı. 5 dakikaydı: T−15/T−10/T−5 slotları 5 dakika arayla olduğu için
        /// tur fazına göre bir slot 5 dakikaya kadar geç çalışıyor ya da T−5 penceresi
        /// kapandıktan sonraya kalıyordu (11.09.2026, Venezia–Fiorentina). Tur başına
        /// sağlayıcı isteği slot kuralıyla sınırlıdır; sık tur yalnız DB okumasıdır.
        /// </summary>
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Kadro penceresinin açılışı — kickoff'a bu kadar kala yoklama BAŞLAR.
        /// Takvimin tek kaynağı <see cref="LineupPollSchedule"/>'dır; burada yalnız
        /// aday sorgusunun tarih aralığı kurulur.
        /// </summary>
        private static readonly TimeSpan LineupLeadTime = LineupPollSchedule.WindowOpen;

        /// <summary>
        /// Kadro yoklamasının UTC gün başına KALICI tavanı (tüm maçlar toplamı).
        ///
        /// Ölçüldü 06.09.2026: /lineups kotanın en pahalı kalemiydi. Pencere T−45'ten
        /// T−90'a genişletildiği için tavan artık ayrıca ve kalıcı olarak tutulur;
        /// mevcut genel bütçe koruması (ApiFootballCacheHandler) üstte devam eder.
        /// </summary>
        private const int DefaultMaxLineupRequestsPerUtcDay = 30;

        /// <summary>
        /// Sakatlık/ceza taramasının ufku. 72 saat iken 3 günlük her fikstür sürekli yoklanıyordu;
        /// oysa sakatlık bilgisi maça 1 günden fazla varken kararı değiştirmiyor. 24 saat, kadro
        /// ufkundan bağımsız kalmaya devam eder ve talebi kaynağında ~3 kat düşürür.
        /// </summary>
        private static readonly TimeSpan StatusLookAhead = TimeSpan.FromHours(12);

        /// <summary>Tur başına sakatlık sorgusu üst sınırı (kota koruması; /injuries 12 saat cache'li).</summary>
        private const int MaxStatusMatchesPerCycle = 4;

        /// <summary>Kickoff'tan sonraki yakalama payı — kaçırılan T−5 slotu bu süre içinde çalışabilir.</summary>
        private static readonly TimeSpan LineupGrace = LineupPollSchedule.CatchUpGrace;

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
            var followUseCase = sp.GetRequiredService<GetUsersFollowingMatchUseCase>();
            var notificationRepo = sp.GetRequiredService<IUserNotificationRepository>();
            var notificationService = sp.GetRequiredService<INotificationService>();

            var utcNow = DateTime.UtcNow;
            // KADRO PENCERESİ: kickoff−90dk … kickoff+10dk yakalama payı (bkz. LineupPollSchedule).
            // Sorgu aralığı bilerek biraz geniştir; KESİN karar takvim sınıfınındır.
            var windowStart = utcNow - LineupGrace;
            var windowEnd = utcNow + LineupLeadTime;

            var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
            // KAPSAM: kadro yalnız FORMAX kapsamındaki maçlar için satın alınır. Filtre API
            // çağrısından ÖNCEdir; kapsam dışı maç için fixtures/lineups isteği = 0.
            var candidates = matchRepo.GetUpcomingMatches(windowStart, windowEnd)
                .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
                .OrderBy(m => m.MatchDate)
                .ToList();

            if (candidates.Count > 0)
            {
                // SLOT KARARI + KALICI DEFTER + DB YAZIMI tek serviste (LineupIngestionService).
                // Restart son kontrol anını sıfırlamaz; harcanmış slot yeniden açılmaz.
                var dailyCap = Math.Max(0, _config.GetValue(
                    "ApiFootball:Lineups:MaxRequestsPerUtcDay", DefaultMaxLineupRequestsPerUtcDay));

                var results = await sp.GetRequiredService<Lineups.LineupIngestionService>()
                    .RunSlotsAsync(candidates, utcNow, dailyCap, ct);

                foreach (var (match, result) in results.Where(r => r.Result.FirstRelease))
                {
                    _logger.LogInformation("[LINEUP JOB] official_lineup_released for match {MatchId}", match.Id);
                    await FanOutNotificationAsync(
                        match, followUseCase, notificationRepo, notificationService, utcNow, ct);
                }

                if (results.Count > 0)
                    _logger.LogInformation(
                        "[LINEUP JOB] {Polled}/{Total} maç için kadro denendi (T−90/60/30/15/10/5): {Outcomes}",
                        results.Count, candidates.Count,
                        string.Join(", ", results.Select(r => r.Match.Id + "=" + r.Result.Outcome)));
            }
            else
            {
                _logger.LogDebug("[LINEUP JOB] no matches in lineup window at {Time}", utcNow);
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
        /// TEK MAÇ için kadro çekimi — teşhis/geri-doldurma tetiği (admin ucundan çağrılır).
        ///
        /// Neden gerekli: döngü yalnız kickoff'a &lt;=60 dk kalan maçları yoklar. Şema
        /// genişlediğinde (formation/grid) DB'de zaten kadrosu olan maçlar bu alanları
        /// boş taşır ve kısa devre yüzünden bir daha hiç çekilmez. <paramref name="force"/>
        /// kısa devreyi atlar; AYNI ingestion kodu çalışır (ayrı bir yol yoktur).
        /// Maç başına 1 sağlayıcı isteği eder — otomatik döngü davranışı DEĞİŞMEZ.
        /// </summary>
        public async Task<bool> RunForMatchAsync(int matchId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
            var match = matchRepo.GetById(matchId);
            if (match == null || string.IsNullOrWhiteSpace(match.ExternalMatchId)) return false;

            using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(LineupIngestionJob));
            var utcNow = DateTime.UtcNow;

            // AYNI yazma yolu (LineupIngestionService) — ayrı bir teşhis kopyası yok.
            var result = await sp.GetRequiredService<Lineups.LineupIngestionService>()
                .FetchAndStoreAsync(match, utcNow, ct);
            if (result.FirstRelease)
                await FanOutNotificationAsync(match,
                    sp.GetRequiredService<GetUsersFollowingMatchUseCase>(),
                    sp.GetRequiredService<IUserNotificationRepository>(),
                    sp.GetRequiredService<INotificationService>(), utcNow, ct);

            await RefreshStatusesForMatchAsync(match,
                sp.GetRequiredService<IMatchPlayerStatusRepository>(),
                sp.GetRequiredService<ISportsDataProvider>(), utcNow, ct);

            return result.Outcome == Lineups.LineupFetchOutcome.Released;
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

        /// <summary>
        /// Tek maçın sakatlık/ceza listesi — yalnız admin teşhis ucundan (RunForMatchAsync).
        /// Döngü yolunda bu iş kendi ufku ve bütçesiyle <see cref="RefreshPlayerStatusesAsync"/>
        /// tarafından görülür. Kadro yazımı <see cref="Lineups.LineupIngestionService"/>'tedir.
        /// </summary>
        private async Task RefreshStatusesForMatchAsync(
            Match match,
            IMatchPlayerStatusRepository statusRepo,
            ISportsDataProvider sportsProvider,
            DateTime utcNow,
            CancellationToken ct)
        {
            var statuses = await sportsProvider.GetPlayerStatusesAsync(match.ExternalMatchId!, ct);
            if (statuses.Count == 0) return;

            var statusEntities = statuses.Select(s => new MatchPlayerStatus
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                TeamId = s.TeamId,
                PlayerName = s.PlayerName,
                Status = s.Status,
                Reason = s.Reason,
                FetchedAt = utcNow
            }).ToList();

            await statusRepo.ReplaceAsync(match.Id, statusEntities, ct);
            await statusRepo.SaveChangesAsync(ct);
        }

        private async Task FanOutNotificationAsync(
            Match match,
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            INotificationService notificationService,
            DateTime utcNow,
            CancellationToken ct)
        {
            var homeName = match.HomeTeam?.Name ?? $"Ev sahibi ({match.HomeTeamId})";
            var awayName = match.AwayTeam?.Name ?? $"Deplasman ({match.AwayTeamId})";

            var title = "İlk 11 Açıklandı";
            var message = $"{homeName} - {awayName} ilk 11'leri açıklandı.";

            // Per-user DB notifications
            var userIds = await followUseCase.ExecuteAsync(match.Id);

            foreach (var userId in userIds)
            {
                try
                {
                    var notification = new UserNotification
                    {
                        UserId = userId,
                        MatchId = match.Id,
                        Title = title,
                        Message = message,
                        IsRead = false,
                        CreatedAt = utcNow
                    };

                    await notificationRepo.AddAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[LINEUP JOB] could not save notification for user {UserId} / match {MatchId}",
                        userId, match.Id);
                }
            }

            // Webhook / future push (NullNotificationService in current config)
            try
            {
                await notificationService.NotifyAsync(match.Id, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[LINEUP JOB] INotificationService.NotifyAsync failed for match {MatchId}", match.Id);
            }
        }
    }
}
