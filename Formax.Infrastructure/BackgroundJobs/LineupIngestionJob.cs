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
    /// Background job that polls the sports data provider every 5 minutes.
    ///
    /// KICKOFF PENCERESİ: yalnız başlamasına <= 60 dakika kalan, HENÜZ BAŞLAMAMIŞ maçlar.
    /// Yani MatchDate ∈ [now, now + 60dk]. Gerekçe (kota): kadrolar tipik olarak maçtan
    /// ~45 dakika önce açıklanır; daha erken sorulan her istek kesin boş döner. Maç
    /// başladıktan sonra da kadro sorulmaz — canlı akış bu job'un işi değildir.
    ///
    /// ÖNCESİ: pencere [now - 60dk, now + 90dk] idi; hem maça 90 dk kala hem de maç
    /// başladıktan 60 dk sonrasına kadar boş yere fixtures/lineups çağrısı yapılıyordu.
    ///
    /// Kadro bir kez açıklandıysa (DB'de her iki taraf da released) o maç için provider'a
    /// hiç gidilmez; sağlayıcıdaki 15 dk negatif cache ve 1 saat pozitif cache korunur.
    ///
    /// On lineup release:
    ///   1. Persists MatchLineup + MatchLineupPlayers
    ///   2. Persists MatchPlayerStatuses
    ///   3. Fan-outs UserNotification to every follower of that match
    ///   4. Calls INotificationService (webhook / future push)
    /// </summary>
    public sealed class LineupIngestionJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(5);

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

        /// <summary>Aynı fikstürün iki yoklaması arasındaki en kısa süre.</summary>
        private static readonly TimeSpan LineupPerFixtureCooldown = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Sakatlık/ceza taramasının ufku. 72 saat iken 3 günlük her fikstür sürekli yoklanıyordu;
        /// oysa sakatlık bilgisi maça 1 günden fazla varken kararı değiştirmiyor. 24 saat, kadro
        /// ufkundan bağımsız kalmaya devam eder ve talebi kaynağında ~3 kat düşürür.
        /// </summary>
        private static readonly TimeSpan StatusLookAhead = TimeSpan.FromHours(12);

        /// <summary>Tur başına sakatlık sorgusu üst sınırı (kota koruması; /injuries 12 saat cache'li).</summary>
        private const int MaxStatusMatchesPerCycle = 4;

        /// <summary>Kickoff'tan sonraki hoşgörü payı — kadro dakikalar kala yayımlanabiliyor.</summary>
        private static readonly TimeSpan LineupGrace = TimeSpan.FromMinutes(10);

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
            var lineupRepo = sp.GetRequiredService<IMatchLineupRepository>();
            var statusRepo = sp.GetRequiredService<IMatchPlayerStatusRepository>();
            var sportsProvider = sp.GetRequiredService<ISportsDataProvider>();
            var followUseCase = sp.GetRequiredService<GetUsersFollowingMatchUseCase>();
            var notificationRepo = sp.GetRequiredService<IUserNotificationRepository>();
            var notificationService = sp.GetRequiredService<INotificationService>();

            var utcNow = DateTime.UtcNow;
            // KADRO PENCERESİ: kickoff−90dk … kickoff−5dk (bkz. LineupPollSchedule).
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
                // KALICI DEFTER — kadro yoklamasının freni burada. Restart bu sayıları
                // sıfırlamaz; harcanmış bir slot yeniden açılmaz.
                var ledger = sp.GetRequiredService<IFixtureSyncRepository>();
                var dailyCap = Math.Max(0, _config.GetValue(
                    "ApiFootball:Lineups:MaxRequestsPerUtcDay", DefaultMaxLineupRequestsPerUtcDay));

                var polled = 0;
                foreach (var match in candidates)
                {
                    ct.ThrowIfCancellationRequested();

                    var extId = match.ExternalMatchId!;
                    var stored = lineupRepo.GetByMatchId(match.Id);
                    var complete = stored?.HomeLineupsReleased == true
                                && stored?.AwayLineupsReleased == true;

                    // Kadro zaten TAMSA sağlayıcıya HİÇ gidilmez — açıklanan kadro değişmez.
                    var attempts = await ledger.GetFixtureAttemptCountAsync(
                        extId, FixtureRefreshPurposes.Lineup, ct);

                    if (!LineupPollSchedule.ShouldPoll(match.MatchDate, utcNow, complete, attempts))
                    {
                        // Kadro tamamlandıysa sakatlık/ceza yolu ayrı ufkuyla zaten çalışır.
                        continue;
                    }

                    // ATOMİK REZERVASYON — istek HTTP'den ÖNCE deftere yazılır.
                    if (!ledger.TryReserveFixtureAttempt(
                            extId, FixtureRefreshPurposes.Lineup,
                            LineupPerFixtureCooldown, dailyCap, utcNow))
                        continue;

                    var outcome = "NoData";
                    try
                    {
                        var released = await ProcessMatchAsync(
                            match, lineupRepo, statusRepo,
                            sportsProvider, followUseCase,
                            notificationRepo, notificationService,
                            utcNow, ct, force: false, includePlayerStatuses: false);
                        if (released) outcome = "Applied";
                        polled++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        outcome = "ProviderError";
                        _logger.LogWarning(ex, "[LINEUP JOB] lineup fetch failed for match {MatchId}", match.Id);
                    }

                    ledger.RecordFixtureAttemptOutcome(
                        extId, FixtureRefreshPurposes.Lineup, utcNow, outcome);
                }

                await ledger.SaveChangesAsync(ct);

                if (polled > 0)
                    _logger.LogInformation(
                        "[LINEUP JOB] {Polled}/{Total} maç için kadro soruldu (T−90…T−5 penceresi).",
                        polled, candidates.Count);
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

            await ProcessMatchAsync(
                match,
                sp.GetRequiredService<IMatchLineupRepository>(),
                sp.GetRequiredService<IMatchPlayerStatusRepository>(),
                sp.GetRequiredService<ISportsDataProvider>(),
                sp.GetRequiredService<GetUsersFollowingMatchUseCase>(),
                sp.GetRequiredService<IUserNotificationRepository>(),
                sp.GetRequiredService<INotificationService>(),
                DateTime.UtcNow, ct, force: true);

            return true;
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
        /// Tek maçın kadro çekimi. Dönüş: sağlayıcı GERÇEK kadro verdi mi?
        ///
        /// <paramref name="includePlayerStatuses"/> false iken sakatlık/ceza sorgusu
        /// YAPILMAZ: döngü yolunda o iş, kendi ufku ve bütçesi olan
        /// <see cref="RefreshPlayerStatusesAsync"/> tarafından zaten görülür. Pencere
        /// T−45'ten T−90'a genişletildiği için buradaki ikinci çağrı, aynı maç için
        /// gereksiz bir sağlayıcı isteği daha üretirdi.
        /// </summary>
        private async Task<bool> ProcessMatchAsync(
            Match match,
            IMatchLineupRepository lineupRepo,
            IMatchPlayerStatusRepository statusRepo,
            ISportsDataProvider sportsProvider,
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            INotificationService notificationService,
            DateTime utcNow,
            CancellationToken ct,
            bool force = false,
            bool includePlayerStatuses = true)
        {
            var lineupReleased = false;
            // ── 1. Fetch lineup from provider ─────────────────────────────────
            // KISA DEVRE: her iki tarafın kadrosu DB'de zaten kayıtlıysa provider'a hiç gidilmez.
            // Kadro açıklandıktan sonra değişmez; tekrar sormak boşa kotadır. (Sakatlık/ceza akışı
            // aşağıda AYNEN devam eder — kendi 4 saatlik cache'i vardır.)
            var storedLineup = lineupRepo.GetByMatchId(match.Id);
            var alreadyComplete = !force
                               && storedLineup?.HomeLineupsReleased == true
                               && storedLineup?.AwayLineupsReleased == true;

            var lineupResult = alreadyComplete
                ? null
                : await sportsProvider.GetOfficialLineupAsync(match.ExternalMatchId!, ct);

            if (alreadyComplete)
            {
                _logger.LogDebug("[LINEUP JOB] lineup already stored for match {MatchId} — provider skipped", match.Id);
            }
            else if (lineupResult == null)
            {
                _logger.LogDebug("[LINEUP JOB] no lineup data for match {MatchId}", match.Id);
            }
            else
            {
                var existing = storedLineup;
                bool firstRelease = existing == null
                    && lineupResult.LineupsAnnounced;
                bool wasAlreadyReleased = existing?.HomeLineupsReleased == true
                    || existing?.AwayLineupsReleased == true;

                // ── 2. Persist MatchLineup header ─────────────────────────────
                var lineup = new MatchLineup
                {
                    MatchId = match.Id,
                    HomeLineupsReleased = lineupResult.HomeStarters.Count > 0,
                    AwayLineupsReleased = lineupResult.AwayStarters.Count > 0,
                    // Sağlayıcının açıkladığı diziliş — takım başına ayrı, olduğu gibi.
                    HomeFormation = lineupResult.HomeFormation,
                    AwayFormation = lineupResult.AwayFormation,
                    ReleasedAt = lineupResult.LineupsAnnounced ? utcNow : null,
                    FetchedAt = utcNow
                };

                await lineupRepo.UpsertAsync(lineup, ct);

                // ── 3. Persist players ────────────────────────────────────────
                var players = new List<MatchLineupPlayer>();

                players.AddRange(MapPlayers(match.Id, "Home", "Starter", lineupResult.HomeStarters));
                players.AddRange(MapPlayers(match.Id, "Home", "Bench", lineupResult.HomeBench));
                players.AddRange(MapPlayers(match.Id, "Away", "Starter", lineupResult.AwayStarters));
                players.AddRange(MapPlayers(match.Id, "Away", "Bench", lineupResult.AwayBench));

                if (players.Count > 0)
                    await lineupRepo.ReplacePlayersAsync(match.Id, players, ct);

                await lineupRepo.SaveChangesAsync(ct);

                // Sağlayıcı GERÇEK kadro verdi mi? Defterin sonucu buna göre yazılır.
                lineupReleased = lineup.HomeLineupsReleased || lineup.AwayLineupsReleased;

                // ── 4. First-release event → notification fan-out ─────────────
                bool triggerNotification = firstRelease
                    || (lineupResult.LineupsAnnounced && !wasAlreadyReleased);

                if (triggerNotification)
                {
                    _logger.LogInformation(
                        "[LINEUP JOB] official_lineup_released for match {MatchId}", match.Id);

                    await FanOutNotificationAsync(
                        match, followUseCase, notificationRepo,
                        notificationService, utcNow, ct);
                }
            }

            // ── 5. Fetch + persist player statuses ────────────────────────────
            // Döngü yolunda ATLANIR (bkz. includePlayerStatuses): sakatlık/ceza kendi
            // ufku ve bütçesiyle ayrıca taranır; burada tekrar sormak aynı maç için
            // ikinci bir sağlayıcı isteği demektir.
            if (!includePlayerStatuses) return lineupReleased;

            var statuses = await sportsProvider.GetPlayerStatusesAsync(
                match.ExternalMatchId!, ct);

            if (statuses.Count > 0)
            {
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

                _logger.LogDebug(
                    "[LINEUP JOB] persisted {Count} player status(es) for match {MatchId}",
                    statusEntities.Count, match.Id);
            }

            return lineupReleased;
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

        private static IEnumerable<MatchLineupPlayer> MapPlayers(
            int matchId,
            string side,
            string role,
            IEnumerable<Application.DTOs.Lineup.SportsLineupPlayer> source)
        {
            return source.Select(p => new MatchLineupPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                Side = side,
                Role = role,
                ShirtNumber = p.ShirtNumber,
                PlayerName = p.Name,
                Position = p.Position,
                Grid = p.Grid,
                IsCaptain = p.IsCaptain
            });
        }
    }
}
