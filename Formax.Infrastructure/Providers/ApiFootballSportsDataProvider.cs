using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Formax.Application.DTOs.Diagnostics;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Odds;
using Formax.Application.DTOs.Players;
using Formax.Application.DTOs.Predictions;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Providers
{
    /// <summary>
    /// Sports data provider backed by api-football.com (v3).
    /// Config keys: ApiFootball:ApiKey, ApiFootball:BaseUrl (optional).
    ///
    /// IMemoryCache TTLs (V1 — single instance):
    ///   Fixtures (batch window)  : 15 min
    ///   Lineup                   : 60 min  (released ~1 h before KO)
    ///   Injuries / statuses      :  4 h    (infrequent changes)
    ///   Standings                : 15 min
    ///   Competition context      : 24 h    (stable)
    ///   Live batch (all matches) : 60 sec
    ///   Live stats (per match)   : 60 sec
    ///   Live events (per match)  : 60 sec
    ///   Live momentum (per match): 60 sec
    /// </summary>
    public class ApiFootballSportsDataProvider : ISportsDataProvider
    {
        private const string DefaultBaseUrl = "https://v3.football.api-sports.io";

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApiFootballSportsDataProvider> _logger;
        private readonly ApiFootballMetrics _metrics;
        private readonly string _apiKey;

        // ── MVP Release Hardening: configurable timezone (default Europe/Istanbul) ──
        // api-football /fixtures ailesine gönderilir; UTC kaynaklı gün kaymasını önler.
        private readonly string _timezone;
        private readonly string _tzQuery;

        // ── Fixture Expansion v2 — takım Timeline pencere boyutları (config) ──
        // GET /fixtures?team=&last=N / &next=N. api-football üst sınır 99; varsayılan 20.
        private readonly int _timelineLastN;
        private readonly int _timelineNextN;

        // ── Cache TTLs ────────────────────────────────────────────────────────
        private static readonly TimeSpan TtlFixtures    = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlLineup      = TimeSpan.FromHours(1);
        // NEGATİF CACHE: kadro henüz açıklanmamış fikstür. Boş yanıt cache'lenmediği için
        // LineupIngestionJob (5 dk döngü, 150 dk pencere) aynı maçı ~30 kez soruyordu.
        // Kısa TTL: kadro açıklandığında en fazla bu kadar gecikmeyle görülür.
        private static readonly TimeSpan TtlLineupEmpty = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlInjuries    = TimeSpan.FromHours(4);
        // NEGATİF CACHE: sağlayıcı GEÇİCİ olarak gövde döndürmediğinde (response.Response == null)
        // aynı fixture kısa süre yeniden sorulmaz. Boş LİSTE zaten normal TTL ile cache'lenir;
        // burada amaç veriyi seyreltmek DEĞİL, "şu an veri yok" halinin tekrarını kesmektir.
        private static readonly TimeSpan TtlInjuriesEmpty = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlStandings   = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlCompCtx     = TimeSpan.FromHours(24);
        private static readonly TimeSpan TtlLive        = TimeSpan.FromSeconds(60);
        // Takım Timeline (geçmiş/gelecek) — geçmiş nadiren, gelecek yavaş değişir.
        // 6h TTL: aynı döngüde tekrar-çağrıyı önler, kotayı korur.
        private static readonly TimeSpan TtlTeamTimeline = TimeSpan.FromHours(6);

        public ApiFootballSportsDataProvider(
            HttpClient http,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<ApiFootballSportsDataProvider> logger,
            ApiFootballMetrics metrics)
        {
            _http   = http;
            _cache  = cache;
            _logger = logger;
            _metrics = metrics;

            _apiKey = configuration["ApiFootball:ApiKey"] ?? string.Empty;

            var baseUrl = configuration["ApiFootball:BaseUrl"] ?? DefaultBaseUrl;
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Add("x-apisports-key", _apiKey);

            // Configurable timezone — tek kaynak appsettings "ApiFootball:Timezone".
            _timezone = Formax.Infrastructure.Http.ApiFootballTimeZone.ResolveId(
                configuration[Formax.Infrastructure.Http.ApiFootballTimeZone.ConfigKey]);
            _tzQuery = "&timezone=" + Uri.EscapeDataString(_timezone);

            // Fixture Expansion v2 — Timeline pencere boyutları (config, güvenli sınırlar).
            _timelineLastN = ClampWindow(configuration["Timeline:LastN"], fallback: 20);
            _timelineNextN = ClampWindow(configuration["Timeline:NextN"], fallback: 20);
        }

        // api-football last/next üst sınırı 99; 1..99 aralığına kelepçele, geçersizde fallback.
        private static int ClampWindow(string? raw, int fallback)
        {
            if (int.TryParse(raw, out var v) && v >= 1 && v <= 99) return v;
            return fallback;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Sprint 0: Fixture sync
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches every fixture in the [from, to] window across all leagues.
        /// api-football only accepts from/to together with league+season, so the
        /// window is pulled one day at a time via GET /fixtures?date=YYYY-MM-DD
        /// (all-league form) and the results are concatenated.
        /// </summary>
        public async Task<List<SportsFixtureResult>> GetFixturesAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping fixture sync");
                return new List<SportsFixtureResult>();
            }

            var from = fromDate.ToString("yyyy-MM-dd");
            var to   = toDate.ToString("yyyy-MM-dd");
            var cacheKey = $"fx:{from}:{to}";

            if (_cache.TryGetValue(cacheKey, out List<SportsFixtureResult>? cached) && cached != null)
                return cached;

            try
            {
                var results = new List<SportsFixtureResult>();

                // api-football: /fixtures?from&to yalnız league+season ile geçerlidir
                // (aksi halde "The From field need another parameter" → 0 sonuç). Tüm ligler
                // için doğru sorgu gün-bazlı /fixtures?date=YYYY-MM-DD'dir; pencereyi gün gün
                // çekip birleştiriyoruz (Pro plan: 7500 istek/gün, ~9 istek/döngü).
                for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
                {
                    var response = await _http.GetFromJsonAsync<ApiFootballFixtureSyncResponse>(
                        $"fixtures?date={day:yyyy-MM-dd}{_tzQuery}", ct);

                    if (response?.Response == null)
                        continue;

                    foreach (var entry in response.Response)
                    {
                        var mapped = MapSyncEntry(entry);
                        if (mapped != null) results.Add(mapped);
                    }
                }

                _logger.LogInformation(
                    "[SPORTS] GetFixturesAsync: {Count} fixture(s) from {From} to {To}",
                    results.Count, from, to);

                _cache.Set(cacheKey, results, TtlFixtures);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[SPORTS] Fixture sync fetch failed for window {From}→{To}",
                    fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));
                return new List<SportsFixtureResult>();
            }
        }

        /// <summary>
        /// Fixture Expansion v2 — a team's last N FINISHED matches via
        /// GET /fixtures?team={id}&last={N} (all competitions). Cached per team (6 h)
        /// so repeated Timeline cycles stay near-free. Empty on failure / no coverage.
        /// </summary>
        public Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
            string externalTeamId, CancellationToken ct = default)
            => GetTeamFixturesAsync(externalTeamId, isPast: true, _timelineLastN, ct);

        /// <summary>
        /// Fixture Expansion v2 — a team's next N UPCOMING fixtures via
        /// GET /fixtures?team={id}&next={N} (all competitions). Cached per team (6 h).
        /// Empty on failure / no scheduled fixtures.
        /// </summary>
        public Task<List<SportsFixtureResult>> GetTeamUpcomingFixturesAsync(
            string externalTeamId, CancellationToken ct = default)
            => GetTeamFixturesAsync(externalTeamId, isPast: false, _timelineNextN, ct);

        /// <summary>
        /// Shared team-fixture fetch for the Timeline legs. <paramref name="isPast"/> selects
        /// the api-football window parameter (last= vs next=). Same fixture-entry shape as the
        /// date-window sync, so mapping is shared via <see cref="MapSyncEntry"/>.
        /// </summary>
        private async Task<List<SportsFixtureResult>> GetTeamFixturesAsync(
            string externalTeamId, bool isPast, int windowN, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping team fixtures for {TeamId}", externalTeamId);
                return new List<SportsFixtureResult>();
            }
            if (string.IsNullOrWhiteSpace(externalTeamId) || !int.TryParse(externalTeamId, out var teamId) || teamId <= 0)
                return new List<SportsFixtureResult>();

            var window   = isPast ? "last" : "next";
            var cacheKey = $"teamfx:{window}:{teamId}:{windowN}";
            if (_cache.TryGetValue(cacheKey, out List<SportsFixtureResult>? cached) && cached != null)
            {
                _metrics.RecordCacheHit(); // kota koruması: cache'ten karşılandı, API isteği yapılmadı
                return cached;
            }

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballFixtureSyncResponse>(
                    $"fixtures?team={teamId}&{window}={windowN}{_tzQuery}", ct);

                var results = new List<SportsFixtureResult>();
                foreach (var entry in response?.Response ?? new())
                {
                    var mapped = MapSyncEntry(entry);
                    if (mapped != null) results.Add(mapped);
                }

                _logger.LogInformation(
                    "[SPORTS] Team {TeamId} {Window}={N}: {Count} fixture(s)",
                    teamId, window, windowN, results.Count);

                _cache.Set(cacheKey, results, TtlTeamTimeline);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[SPORTS] Team fixtures fetch failed for team {TeamId} ({Window})", teamId, window);
                return new List<SportsFixtureResult>();
            }
        }

        /// <summary>
        /// Maps a single api-football fixture entry to <see cref="SportsFixtureResult"/>.
        /// Shared by the date-window sync and the team-Timeline fetches. Returns null when
        /// the entry lacks a usable fixture id or a parseable date.
        /// </summary>
        private SportsFixtureResult? MapSyncEntry(ApiFootballFixtureSyncEntry entry)
        {
            var fixtureId = entry.Fixture?.Id;
            if (fixtureId == null) return null;

            if (!DateTime.TryParse(entry.Fixture?.Date, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var matchDate))
                return null;

            return new SportsFixtureResult
            {
                ExternalMatchId    = fixtureId.Value.ToString(),
                MatchDate          = matchDate.ToUniversalTime(),
                Status             = NormaliseFixtureStatus(entry.Fixture?.Status?.Short),
                LeagueName         = entry.League?.Name ?? string.Empty,
                LeagueExternalId   = entry.League?.Id ?? 0,
                Round              = entry.League?.Round ?? string.Empty,
                HomeTeamExternalId = (entry.Teams?.Home?.Id ?? 0).ToString(),
                HomeTeamName       = entry.Teams?.Home?.Name ?? string.Empty,
                HomeLogoUrl        = entry.Teams?.Home?.Logo,
                AwayTeamExternalId = (entry.Teams?.Away?.Id ?? 0).ToString(),
                AwayTeamName       = entry.Teams?.Away?.Name ?? string.Empty,
                AwayLogoUrl        = entry.Teams?.Away?.Logo,
                Referee            = string.IsNullOrWhiteSpace(entry.Fixture?.Referee)
                                     ? null
                                     : entry.Fixture.Referee,
                Venue              = entry.Fixture?.Venue?.Name,
                HomeScore          = entry.Goals?.Home,
                AwayScore          = entry.Goals?.Away,
                // İLK YARI — sağlayıcı score.halftime altında veriyor (doğrulandı:
                // fixture 1584375 → halftime 2-1, fulltime 3-3). Nullable taşınır:
                // null = sağlayıcı vermedi, 0 = gerçek sıfır.
                HalfTimeHomeScore  = entry.Score?.Halftime?.Home,
                HalfTimeAwayScore  = entry.Score?.Halftime?.Away
            };
        }

        private static string NormaliseFixtureStatus(string? raw)
        {
            return raw?.ToUpperInvariant() switch
            {
                "NS"   => "NotStarted",
                "TBD"  => "NotStarted",
                "1H"   => "Live",
                "HT"   => "Live",
                "2H"   => "Live",
                "ET"   => "Live",
                "BT"   => "Live",
                "P"    => "Live",
                "SUSP" => "Live",
                "INT"  => "Live",
                "LIVE" => "Live",
                "FT"   => "Finished",
                "AET"  => "Finished",
                "PEN"  => "Finished",
                "PST"  => "Postponed",
                "CANC" => "Cancelled",
                "ABD"  => "Cancelled",
                _      => "NotStarted"
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lineup
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsLineupResult?> GetOfficialLineupAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping lineup fetch for fixture {FixtureId}", matchExternalId);
                return null;
            }

            var cacheKey = $"lineup:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsLineupResult? cachedLineup) && cachedLineup != null)
            {
                _metrics.RecordCacheHit();
                return cachedLineup;
            }

            // Kadro açıklanmadığı bilinen fikstür — TTL dolana dek API'ye gidilmez (kota koruması).
            // Sonuç yine null'dır: sahte/boş kadro ÜRETİLMEZ, DB'ye hiçbir şey yazılmaz.
            var emptyCacheKey = $"lineup:empty:{matchExternalId}";
            if (_cache.TryGetValue(emptyCacheKey, out bool _))
            {
                _metrics.RecordCacheHit();
                return null;
            }

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballLineupResponse>(
                    $"fixtures/lineups?fixture={matchExternalId}",
                    ct);

                if (response?.Response == null || response.Response.Count == 0)
                {
                    _cache.Set(emptyCacheKey, true, TtlLineupEmpty);
                    return null;
                }

                // Two team entries: index 0 = home, index 1 = away (per api-football spec)
                var home = response.Response.ElementAtOrDefault(0);
                var away = response.Response.ElementAtOrDefault(1);

                if (home == null && away == null)
                {
                    _cache.Set(emptyCacheKey, true, TtlLineupEmpty);
                    return null;
                }

                var result = new SportsLineupResult
                {
                    LineupsAnnounced = home?.StartXI?.Count > 0 || away?.StartXI?.Count > 0,
                    // Diziliş her takım için AYRI taşınır — biri diğerine kopyalanmaz.
                    HomeFormation = home?.Formation,
                    AwayFormation = away?.Formation,
                    HomeStarters = MapPlayers(home?.StartXI),
                    HomeBench = MapPlayers(home?.Substitutes),
                    AwayStarters = MapPlayers(away?.StartXI),
                    AwayBench = MapPlayers(away?.Substitutes)
                };

                // Kadro geldi → "boş" işareti artık geçersiz.
                _cache.Remove(emptyCacheKey);
                _cache.Set(cacheKey, result, TtlLineup);
                return result;
            }
            catch (Exception ex)
            {
                // Hata negatif cache'lenMEZ: geçici arıza/rate-limit sonrası tekrar denenmelidir.
                _logger.LogError(ex, "[SPORTS] Lineup fetch failed for fixture {FixtureId}", matchExternalId);
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Player statuses (injuries / suspensions)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsPlayerStatusResult>> GetPlayerStatusesAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping status fetch for fixture {FixtureId}", matchExternalId);
                return new List<SportsPlayerStatusResult>();
            }

            var cacheKey = $"injuries:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out List<SportsPlayerStatusResult>? cachedStatuses) && cachedStatuses != null)
            {
                _metrics.RecordCacheHit();
                return cachedStatuses;
            }

            // Geçici olarak gövdesiz yanıt alınan fixture — TTL dolana dek tekrar sorulmaz.
            // Sonuç yine boş listedir: sahte sakatlık verisi ÜRETİLMEZ.
            var emptyCacheKey = $"injuries:empty:{matchExternalId}";
            if (_cache.TryGetValue(emptyCacheKey, out bool _))
            {
                _metrics.RecordCacheHit();
                return new List<SportsPlayerStatusResult>();
            }

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballInjuryResponse>(
                    $"injuries?fixture={matchExternalId}",
                    ct);

                if (response?.Response == null)
                {
                    _cache.Set(emptyCacheKey, true, TtlInjuriesEmpty);
                    return new List<SportsPlayerStatusResult>();
                }

                // TEKİLLEŞTİRME: /injuries aynı oyuncu için birden çok satır döndürebilir
                // (üretimde ölçüldü: 300 satır / 150 tekil oyuncu → her kayıt tam 2×).
                // Tekilleştirilmezse Availability eksik sayısı ikiye katlanır. Aynı desen
                // GetTeamInjuriesAsync'te (injuries?team=) ZATEN uygulanıyor — burada eksikti.
                var result = Dedupe(response.Response
                    .Select(r => new SportsPlayerStatusResult
                    {
                        PlayerId = r.Player?.Id ?? 0,
                        PlayerName = r.Player?.Name ?? string.Empty,
                        TeamId = r.Team?.Id ?? 0,
                        Status = NormaliseStatus(r.Player?.Type, r.Player?.Reason),
                        Reason = r.Player?.Reason ?? string.Empty
                    })
                    .Where(r => !string.IsNullOrWhiteSpace(r.PlayerName)));

                // Gerçek yanıt geldi → "veri yok" işareti geçersiz.
                _cache.Remove(emptyCacheKey);
                _cache.Set(cacheKey, result, TtlInjuries);
                return result;
            }
            catch (Exception ex)
            {
                // Hata negatif cache'lenMEZ: geçici arıza/rate-limit sonrası tekrar denenmelidir.
                _logger.LogError(ex, "[SPORTS] Status fetch failed for fixture {FixtureId}", matchExternalId);
                return new List<SportsPlayerStatusResult>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static List<SportsLineupPlayer> MapPlayers(
            List<ApiFootballPlayerEntry>? entries)
        {
            if (entries == null) return new List<SportsLineupPlayer>();

            return entries.Select(e => new SportsLineupPlayer
            {
                Name = e.Player?.Name ?? string.Empty,
                ShirtNumber = e.Player?.Number ?? 0,
                Position = e.Player?.Pos ?? string.Empty,
                // Sağlayıcı değeri OLDUĞU GİBİ taşınır (yorumlanmaz, üretilmez).
                Grid = e.Player?.Grid,
                IsCaptain = e.Player?.Captain == "1" || e.Player?.Captain == "true"
            }).ToList();
        }

        /// <summary>
        /// api-football kaydını FORMAX status'una çevirir ("Injured" | "Suspended" | "Doubtful").
        ///
        /// <paramref name="type"/> oyuncunun O MAÇTAKİ durumudur ("Missing Fixture" = kesin yok,
        /// "Questionable" = şüpheli); <paramref name="reason"/> ise SEBEBİ ("Knee Injury",
        /// "Red Card"…). Önceki sürüm yalnız reason'a bakıyordu ve iki hata üretiyordu:
        ///   • "Questionable" kayıtlar reason'ı "Injury" ise kesin eksik sayılıyordu,
        ///   • kart cezaları ("Red Card" / "Yellow Cards") "Doubtful"a düşüp eksik SAYILMIYORDU
        ///     (üretimde 26 satır) — oysa cezalı oyuncu kesinlikle oynayamaz.
        ///
        /// KASITLI OLARAK YAPILMAYAN: "Missing Fixture" tek başına eksik SAYILMAZ. Sebep
        /// bilinmiyorsa (ör. "Inactive") sağlayıcının ne kastettiği doğrulanamadığı için
        /// eski davranış korunur — tahminle eksik üretilmez.
        /// </summary>
        private static string NormaliseStatus(string? type, string? reason)
        {
            // "Questionable" → sebep ne olursa olsun KESİN eksik değildir.
            if ((type ?? string.Empty).ToLowerInvariant().Contains("questionable"))
                return "Doubtful";

            if (string.IsNullOrWhiteSpace(reason)) return "Doubtful";

            var lower = reason.ToLowerInvariant();
            // Ceza: doğrudan "suspended" ya da kart birikimi/kırmızı kart.
            if (lower.Contains("suspend") || lower.Contains("card")) return "Suspended";
            if (lower.Contains("injur") || lower.Contains("miss")) return "Injured";
            return "Doubtful";
        }

        /// <summary>
        /// Oyuncu bazında tekilleştirir. Kimlik önceliği: api-football PlayerId; sağlayıcı id
        /// vermediyse (0) takım + normalize ad. Aynı oyuncunun birden çok kaydı varsa EN AĞIR
        /// durum kazanır → kesin eksik, "Doubtful" bir kopya yüzünden kaybolmaz.
        /// </summary>
        private static List<SportsPlayerStatusResult> Dedupe(IEnumerable<SportsPlayerStatusResult> source)
        {
            static int Severity(string status) => status switch
            {
                "Suspended" => 3,
                "Injured" => 2,
                _ => 1          // Doubtful
            };

            var best = new Dictionary<string, SportsPlayerStatusResult>(StringComparer.Ordinal);
            foreach (var s in source)
            {
                var key = s.PlayerId > 0
                    ? $"id:{s.PlayerId}"
                    : $"nm:{s.TeamId}|{s.PlayerName.Trim().ToLowerInvariant()}";

                if (best.TryGetValue(key, out var cur) && Severity(cur.Status) >= Severity(s.Status))
                    continue;

                best[key] = s;
            }
            return best.Values.ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Standings
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsStandingEntry>> GetLeagueStandingsAsync(
            string leagueExternalId,
            int season,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping standings fetch for league {LeagueId}", leagueExternalId);
                return new List<SportsStandingEntry>();
            }

            var cacheKey = $"standings:{leagueExternalId}:{season}";
            if (_cache.TryGetValue(cacheKey, out List<SportsStandingEntry>? cachedStandings) && cachedStandings != null)
                return cachedStandings;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballStandingsResponse>(
                    $"standings?league={leagueExternalId}&season={season}",
                    ct);

                var groups = response?.Response
                    ?.FirstOrDefault()
                    ?.League
                    ?.Standings;

                if (groups == null || groups.Count == 0)
                    return new List<SportsStandingEntry>();

                // api-football returns an array of groups; take the first (main table)
                var result = groups[0].Select(r => new SportsStandingEntry
                {
                    TeamId = r.Team?.Id ?? 0,
                    TeamName = r.Team?.Name ?? string.Empty,
                    Position = r.Rank,
                    Played = r.All?.Played ?? 0,
                    Won = r.All?.Win ?? 0,
                    Drawn = r.All?.Draw ?? 0,
                    Lost = r.All?.Lose ?? 0,
                    GoalsFor = r.All?.Goals?.For ?? 0,
                    GoalsAgainst = r.All?.Goals?.Against ?? 0,
                    Points = r.Points ?? 0,
                    Form = r.Form ?? string.Empty
                })
                .Where(r => r.TeamId > 0)
                .ToList();

                _cache.Set(cacheKey, result, TtlStandings);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Standings fetch failed for league {LeagueId} season {Season}", leagueExternalId, season);
                return new List<SportsStandingEntry>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Competition context
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsCompetitionContext?> GetCompetitionContextAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping context fetch for fixture {FixtureId}", matchExternalId);
                return null;
            }

            var cacheKey = $"compctx:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsCompetitionContext? cachedCtx) && cachedCtx != null)
                return cachedCtx;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballFixtureResponse>(
                    $"fixtures?id={matchExternalId}{_tzQuery}",
                    ct);

                var fixture = response?.Response?.FirstOrDefault();
                if (fixture?.League == null)
                    return null;

                var stageName = fixture.League.Round ?? string.Empty;
                var compType = NormaliseCompetitionType(fixture.League.Type, stageName);

                var result = new SportsCompetitionContext
                {
                    CompetitionType = compType,
                    StageName = stageName,
                    LeagueName = fixture.League.Name ?? string.Empty
                };

                _cache.Set(cacheKey, result, TtlCompCtx);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Competition context fetch failed for fixture {FixtureId}", matchExternalId);
                return null;
            }
        }

        // api-football league.type YALNIZ "League" | "Cup" verir; Knockout ayrımı ROUND metninden
        // türetilir (generic keyword; takım/lig hardcode'u YOK). Cup + eleme-round → "Knockout".
        private static string NormaliseCompetitionType(string? rawType, string? round)
        {
            var isCup = string.Equals(rawType, "Cup", StringComparison.OrdinalIgnoreCase);
            if (!isCup) return "League";

            var r = (round ?? string.Empty).ToLowerInvariant();
            var isKnockout = r.Contains("final") || r.Contains("semi") || r.Contains("quarter")
                          || r.Contains("knockout") || r.Contains("round of") || r.Contains("1/")
                          || r.Contains("play-off") || r.Contains("playoff");
            return isKnockout ? "Knockout" : "Cup";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Phase 6 — Team season statistics (/teams/statistics)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsTeamStatistics?> GetTeamSeasonStatisticsAsync(
            string leagueExternalId,
            string teamExternalId,
            int season,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping team stats for team {TeamId}", teamExternalId);
                return null;
            }
            if (string.IsNullOrWhiteSpace(leagueExternalId) || string.IsNullOrWhiteSpace(teamExternalId))
                return null;

            var cacheKey = $"teamstats:{leagueExternalId}:{season}:{teamExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsTeamStatistics? cached) && cached != null)
                return cached;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballTeamStatsResponse>(
                    $"teams/statistics?league={leagueExternalId}&season={season}&team={teamExternalId}", ct);

                var r = response?.Response;
                // Coverage yoksa api-football boş bir gövde döndürebilir; oynanan maç yoksa veri yok.
                if (r?.Team?.Id == null || (r.Fixtures?.Played?.Total ?? 0) <= 0)
                    return null;

                static double ParseAvg(string? s)
                    => double.TryParse(s, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;

                var result = new SportsTeamStatistics
                {
                    TeamId               = r.Team.Id.Value,
                    TeamName             = r.Team.Name ?? string.Empty,
                    PlayedTotal          = r.Fixtures?.Played?.Total ?? 0,
                    PlayedHome           = r.Fixtures?.Played?.Home ?? 0,
                    PlayedAway           = r.Fixtures?.Played?.Away ?? 0,
                    WinsTotal            = r.Fixtures?.Wins?.Total ?? 0,
                    DrawsTotal           = r.Fixtures?.Draws?.Total ?? 0,
                    LosesTotal           = r.Fixtures?.Loses?.Total ?? 0,
                    GoalsForAvgTotal     = ParseAvg(r.Goals?.For?.Average?.Total),
                    GoalsForAvgHome      = ParseAvg(r.Goals?.For?.Average?.Home),
                    GoalsForAvgAway      = ParseAvg(r.Goals?.For?.Average?.Away),
                    GoalsAgainstAvgTotal = ParseAvg(r.Goals?.Against?.Average?.Total),
                    GoalsAgainstAvgHome  = ParseAvg(r.Goals?.Against?.Average?.Home),
                    GoalsAgainstAvgAway  = ParseAvg(r.Goals?.Against?.Average?.Away),
                    CleanSheetTotal      = r.CleanSheet?.Total ?? 0,
                    FailedToScoreTotal   = r.FailedToScore?.Total ?? 0,
                    Form                 = r.Form ?? string.Empty
                };

                _cache.Set(cacheKey, result, TtlStandings);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Team stats fetch failed for team {TeamId} league {LeagueId}", teamExternalId, leagueExternalId);
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Phase 6 / Slice 2 — Match prediction (/predictions?fixture=)  [AI-only]
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsMatchPrediction?> GetMatchPredictionAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping prediction for fixture {FixtureId}", matchExternalId);
                return null;
            }
            if (string.IsNullOrWhiteSpace(matchExternalId))
                return null;

            var cacheKey = $"prediction:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsMatchPrediction? cached) && cached != null)
                return cached;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballPredictionResponse>(
                    $"predictions?fixture={matchExternalId}", ct);

                var body = response?.Response?.FirstOrDefault();
                var pred = body?.Predictions;
                if (pred?.Percent == null)
                    return null; // coverage yok → sinyal yok

                static int ParsePct(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return 0;
                    var t = s.Replace("%", "").Trim();
                    // Yüzdeler int ("50%") veya ondalık ("84.5%") olabilir → double parse + round.
                    return double.TryParse(t, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v)
                        ? (int)Math.Round(v) : 0;
                }

                var pctHome = ParsePct(pred.Percent.Home);
                var pctDraw = ParsePct(pred.Percent.Draw);
                var pctAway = ParsePct(pred.Percent.Away);

                // Coverage boşsa api-football tüm yüzdeleri 0 döndürebilir → sinyal yok.
                if (pctHome == 0 && pctDraw == 0 && pctAway == 0)
                    return null;

                var homeId = body?.Teams?.Home?.Id;
                var awayId = body?.Teams?.Away?.Id;
                var winnerId = pred.Winner?.Id;
                var side = winnerId.HasValue && homeId.HasValue && winnerId == homeId ? "Home"
                         : winnerId.HasValue && awayId.HasValue && winnerId == awayId ? "Away"
                         : string.Empty;

                var result = new SportsMatchPrediction
                {
                    PercentHome         = pctHome,
                    PercentDraw         = pctDraw,
                    PercentAway         = pctAway,
                    WinnerName          = pred.Winner?.Name ?? string.Empty,
                    WinnerSide          = side,
                    WinOrDraw           = pred.WinOrDraw ?? false,
                    Advice              = pred.Advice ?? string.Empty,
                    UnderOver           = pred.UnderOver ?? string.Empty,
                    ComparisonTotalHome = ParsePct(body?.Comparison?.Total?.Home),
                    ComparisonTotalAway = ParsePct(body?.Comparison?.Total?.Away)
                };

                _cache.Set(cacheKey, result, TtlStandings);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Prediction fetch failed for fixture {FixtureId}", matchExternalId);
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Phase 6 Final — Team profile bundle (/coachs + /teams + /players/squads + /transfers)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsTeamProfile?> GetTeamProfileAsync(
            string teamExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping team profile for {TeamId}", teamExternalId);
                return null;
            }
            if (string.IsNullOrWhiteSpace(teamExternalId) || !int.TryParse(teamExternalId, out var teamId))
                return null;

            var cacheKey = $"teamprofile:{teamExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsTeamProfile? cached) && cached != null)
                return cached;

            var profile = new SportsTeamProfile();

            // ── Coach (/coachs?team=) ────────────────────────────────────────────
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballCoachResponse>(
                    $"coachs?team={teamId}", ct);
                var current = resp?.Response?.FirstOrDefault(c => c.Team?.Id == teamId)
                              ?? resp?.Response?.FirstOrDefault();
                if (current != null && !string.IsNullOrWhiteSpace(current.Name))
                {
                    profile.HasCoach = true;
                    profile.CoachName = current.Name!;
                    profile.CoachAge = current.Age ?? 0;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[SPORTS] coach fetch failed for team {TeamId}", teamId); }

            // ── Venue (/teams?id=) ───────────────────────────────────────────────
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballTeamsInfoResponse>(
                    $"teams?id={teamId}", ct);
                var v = resp?.Response?.FirstOrDefault()?.Venue;
                if (v != null && !string.IsNullOrWhiteSpace(v.Name))
                {
                    profile.HasVenue = true;
                    profile.VenueName = v.Name!;
                    profile.VenueCity = v.City ?? string.Empty;
                    profile.VenueCapacity = v.Capacity ?? 0;
                    profile.VenueSurface = v.Surface ?? string.Empty;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[SPORTS] venue fetch failed for team {TeamId}", teamId); }

            // ── Squad (/players/squads?team=) ────────────────────────────────────
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballSquadResponse>(
                    $"players/squads?team={teamId}", ct);
                var players = resp?.Response?.FirstOrDefault()?.Players;
                if (players is { Count: > 0 })
                {
                    var ages = players.Where(p => (p.Age ?? 0) > 0).Select(p => p.Age!.Value).ToList();
                    profile.HasSquad = true;
                    profile.SquadSize = players.Count;
                    profile.SquadAvgAge = ages.Count > 0 ? Math.Round(ages.Average(), 1) : 0.0;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[SPORTS] squad fetch failed for team {TeamId}", teamId); }

            // ── Transfers (/transfers?team=) — son 365 gün, bu takım perspektifinden ──
            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballTransfersResponse>(
                    $"transfers?team={teamId}", ct);
                var cutoff = DateTime.UtcNow.AddDays(-365);
                int tin = 0, tout = 0; var any = false;
                foreach (var item in resp?.Response ?? new())
                {
                    foreach (var mv in item.Transfers ?? new())
                    {
                        if (!DateTime.TryParse(mv.Date, null,
                                System.Globalization.DateTimeStyles.AssumeUniversal, out var d)) continue;
                        if (d < cutoff) continue;
                        if (mv.Teams?.In?.Id == teamId) { tin++; any = true; }
                        else if (mv.Teams?.Out?.Id == teamId) { tout++; any = true; }
                    }
                }
                if (resp?.Response is { Count: > 0 })
                {
                    profile.HasTransfers = any || true; // provider takımı tanıyor → coverage var (0 son transfer de gerçek sinyal)
                    profile.RecentTransfersIn = tin;
                    profile.RecentTransfersOut = tout;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[SPORTS] transfers fetch failed for team {TeamId}", teamId); }

            // Hiçbir blok dolmadıysa coverage yok → null (fake yazma).
            if (!profile.HasCoach && !profile.HasVenue && !profile.HasSquad && !profile.HasTransfers)
                return null;

            _cache.Set(cacheKey, profile, TtlCompCtx);
            return profile;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Batch live endpoint — single call for ALL live fixtures
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calls GET /fixtures?live=all — one request per cycle regardless of how many
        /// matches are live.  Returns basic score + clock only; no detailed statistics.
        /// </summary>
        public async Task<List<SportsLiveBatchEntry>> GetAllLiveFixturesAsync(
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping batch live fetch");
                return new List<SportsLiveBatchEntry>();
            }

            const string cacheKey = "live:batch";
            if (_cache.TryGetValue(cacheKey, out List<SportsLiveBatchEntry>? cachedBatch) && cachedBatch != null)
                return cachedBatch;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballLiveFixtureResponse>(
                    "fixtures?live=all" + _tzQuery, ct);

                if (response?.Response == null)
                    return new List<SportsLiveBatchEntry>();

                var result = response.Response
                    .Where(e => e.Fixture?.Id != null)
                    .Select(e => new SportsLiveBatchEntry
                    {
                        ExternalMatchId = e.Fixture!.Id!.Value.ToString(),
                        HomeScore = e.Goals?.Home ?? 0,
                        AwayScore = e.Goals?.Away ?? 0,
                        Minute = e.Fixture.Status?.Elapsed,
                        Phase = e.Fixture.Status?.Short ?? string.Empty
                    })
                    .ToList();

                _cache.Set(cacheKey, result, TtlLive);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Batch live fixtures fetch failed");
                return new List<SportsLiveBatchEntry>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Live stats
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsLiveStats?> GetLiveMatchStatsAsync(
            string matchExternalId,
            SportsLiveBatchEntry? batchEntry,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping live stats for fixture {FixtureId}", matchExternalId);
                return null;
            }

            var cacheKey = $"livestats:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out SportsLiveStats? cachedLiveStats) && cachedLiveStats != null)
                return cachedLiveStats;

            try
            {
                int homeScore, awayScore;
                int? minute;
                string phase;

                if (batchEntry != null)
                {
                    // Caller already has score + clock from the batch endpoint —
                    // skip GET /fixtures?id= entirely (saves 1 request per changed match).
                    homeScore = batchEntry.HomeScore;
                    awayScore = batchEntry.AwayScore;
                    minute    = batchEntry.Minute;
                    phase     = batchEntry.Phase;
                }
                else
                {
                    // No batch entry available (e.g. called from GetLiveMomentumAsync) —
                    // fall back to the per-fixture endpoint.
                    var fixtureResp = await _http.GetFromJsonAsync<ApiFootballLiveFixtureResponse>(
                        $"fixtures?id={matchExternalId}{_tzQuery}", ct);

                    var fixture = fixtureResp?.Response?.FirstOrDefault();
                    if (fixture == null) return null;

                    homeScore = fixture.Goals?.Home ?? 0;
                    awayScore = fixture.Goals?.Away ?? 0;
                    minute    = fixture.Fixture?.Status?.Elapsed;
                    phase     = fixture.Fixture?.Status?.Short ?? string.Empty;
                }

                // Fetch detailed statistics
                var statsResp = await _http.GetFromJsonAsync<ApiFootballStatisticsResponse>(
                    $"fixtures/statistics?fixture={matchExternalId}", ct);

                // api-football: index 0 = home, index 1 = away
                var homeSt = statsResp?.Response?.ElementAtOrDefault(0)?.Statistics;
                var awaySt = statsResp?.Response?.ElementAtOrDefault(1)?.Statistics;

                static int GetInt(List<ApiFootballStatEntry>? list, string type)
                {
                    var raw = list?.FirstOrDefault(s => s.Type == type)?.Value;
                    if (raw == null) return 0;
                    if (raw is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
                            return je.GetInt32();
                        if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = je.GetString()?.Replace("%", "").Trim();
                            return int.TryParse(s, out var v) ? v : 0;
                        }
                    }
                    return 0;
                }

                static double? GetDouble(List<ApiFootballStatEntry>? list, string type)
                {
                    var raw = list?.FirstOrDefault(s => s.Type == type)?.Value;
                    if (raw == null) return null;
                    if (raw is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
                            return je.GetDouble();
                        if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = je.GetString()?.Trim();
                            return double.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
                        }
                    }
                    return null;
                }

                var liveResult = new SportsLiveStats
                {
                    HomeScore = homeScore,
                    AwayScore = awayScore,
                    Minute    = minute,
                    Phase     = phase,
                    PossessionHome = GetInt(homeSt, "Ball Possession"),
                    PossessionAway = GetInt(awaySt, "Ball Possession"),
                    ShotsHome = GetInt(homeSt, "Total Shots"),
                    ShotsAway = GetInt(awaySt, "Total Shots"),
                    ShotsOnTargetHome = GetInt(homeSt, "Shots on Goal"),
                    ShotsOnTargetAway = GetInt(awaySt, "Shots on Goal"),
                    CornersHome = GetInt(homeSt, "Corner Kicks"),
                    CornersAway = GetInt(awaySt, "Corner Kicks"),
                    FoulsHome = GetInt(homeSt, "Fouls"),
                    FoulsAway = GetInt(awaySt, "Fouls"),
                    OffsidesHome = GetInt(homeSt, "Offsides"),
                    OffsidesAway = GetInt(awaySt, "Offsides"),
                    YellowHome = GetInt(homeSt, "Yellow Cards"),
                    YellowAway = GetInt(awaySt, "Yellow Cards"),
                    RedHome = GetInt(homeSt, "Red Cards"),
                    RedAway = GetInt(awaySt, "Red Cards"),
                    DangerousAttacksHome = GetInt(homeSt, "Dangerous Attacks"),
                    DangerousAttacksAway = GetInt(awaySt, "Dangerous Attacks"),
                    XgHome = GetDouble(homeSt, "expected_goals"),
                    XgAway = GetDouble(awaySt, "expected_goals")
                };

                _cache.Set(cacheKey, liveResult, TtlLive);
                return liveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Live stats fetch failed for fixture {FixtureId}", matchExternalId);
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Live events
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsLiveEvent>> GetLiveMatchEventsAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[SPORTS] API key not configured — skipping events for fixture {FixtureId}", matchExternalId);
                return new List<SportsLiveEvent>();
            }

            var cacheKey = $"liveevents:{matchExternalId}";
            if (_cache.TryGetValue(cacheKey, out List<SportsLiveEvent>? cachedEvents) && cachedEvents != null)
                return cachedEvents;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballEventsResponse>(
                    $"fixtures/events?fixture={matchExternalId}", ct);

                if (response?.Response == null)
                    return new List<SportsLiveEvent>();

                var result = response.Response
                    .Select(e => new SportsLiveEvent
                    {
                        // Field coverage: time.extra (uzatma dk) dahil — 90+4 → 94 (aksi halde 90'a düşerdi).
                        Minute = (e.Time?.Elapsed ?? 0) + (e.Time?.Extra ?? 0),
                        EventType = NormaliseEventType(e.Type),
                        TeamName = e.Team?.Name ?? string.Empty,
                        PlayerName = e.Player?.Name ?? string.Empty,
                        AssistName = e.Assist?.Name ?? string.Empty,
                        Detail = e.Detail ?? string.Empty
                    })
                    .ToList();

                _cache.Set(cacheKey, result, TtlLive);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPORTS] Events fetch failed for fixture {FixtureId}", matchExternalId);
                return new List<SportsLiveEvent>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Derived momentum
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsLiveMomentum?> GetLiveMomentumAsync(
            string matchExternalId,
            CancellationToken ct = default)
        {
            // No batch entry here — pass null so the method fetches fixture data itself.
            var stats = await GetLiveMatchStatsAsync(matchExternalId, null, ct);
            if (stats == null) return null;

            return DeriveMomentum(stats, stats.Minute ?? 0);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Timeline Operations — GET /status (gerçek plan limiti + günlük kullanım)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<SportsApiStatus?> GetApiStatusAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return null;

            const string cacheKey = "apistatus:v1";
            if (_cache.TryGetValue(cacheKey, out SportsApiStatus? cached) && cached != null)
                return cached;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballStatusResponse>("status", ct);
                var reqs = response?.Response?.Requests;
                var sub  = response?.Response?.Subscription;
                if (reqs == null) return null;

                var result = new SportsApiStatus
                {
                    RequestsToday = reqs.Current ?? 0,
                    DailyLimit    = reqs.LimitDay ?? 0,
                    PlanName      = sub?.Plan ?? string.Empty
                };
                _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SPORTS] /status fetch failed");
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Football Intelligence v1.0 — team players (/players?team=&season=) + injuries
        // ──────────────────────────────────────────────────────────────────────

        private const int MaxPlayerPages = 3; // kota koruması (~60 oyuncu yeterli)

        public async Task<List<SportsPlayerSeasonStat>> GetTeamPlayersAsync(
            string teamExternalId, int season, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return new List<SportsPlayerSeasonStat>();
            if (!int.TryParse(teamExternalId, out var teamId) || teamId <= 0) return new List<SportsPlayerSeasonStat>();

            var cacheKey = $"teamplayers:{teamId}:{season}";
            if (_cache.TryGetValue(cacheKey, out List<SportsPlayerSeasonStat>? cached) && cached != null)
            {
                _metrics.RecordCacheHit();
                return cached;
            }

            var byId = new Dictionary<int, PlayerAcc>();
            try
            {
                int page = 1, totalPages = 1;
                do
                {
                    var resp = await _http.GetFromJsonAsync<ApiFootballPlayersResponse>(
                        $"players?team={teamId}&season={season}&page={page}", ct);
                    totalPages = resp?.Paging?.Total ?? 1;

                    foreach (var item in resp?.Response ?? new())
                    {
                        var pid = item.Player?.Id ?? 0;
                        if (pid == 0) continue;
                        if (!byId.TryGetValue(pid, out var acc))
                            byId[pid] = acc = new PlayerAcc { Id = pid, Name = item.Player?.Name ?? "" };

                        foreach (var st in item.Statistics ?? new())
                        {
                            acc.Goals   += st.Goals?.Total ?? 0;
                            acc.Assists += st.Goals?.Assists ?? 0;
                            acc.Minutes += st.Games?.Minutes ?? 0;
                            acc.Apps    += st.Games?.Appearences ?? 0;
                            acc.Yellow  += st.Cards?.Yellow ?? 0;
                            acc.Red     += st.Cards?.Red ?? 0;
                            acc.Shots   += st.Shots?.Total ?? 0;
                            acc.KeyPasses += st.Passes?.Key ?? 0;
                            if (string.IsNullOrEmpty(acc.Position) && !string.IsNullOrWhiteSpace(st.Games?.Position))
                                acc.Position = st.Games!.Position!;
                            if (double.TryParse(st.Games?.Rating, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var r) && r > 0)
                            { acc.RatingSum += r; acc.RatingCount++; }
                        }
                    }
                    page++;
                } while (page <= totalPages && page <= MaxPlayerPages && !ct.IsCancellationRequested);

                var result = byId.Values.Select(a => new SportsPlayerSeasonStat
                {
                    PlayerId = a.Id, Name = a.Name, Position = a.Position,
                    Goals = a.Goals, Assists = a.Assists, Minutes = a.Minutes,
                    Appearances = a.Apps, Yellow = a.Yellow, Red = a.Red,
                    Shots = a.Shots, KeyPasses = a.KeyPasses,
                    Rating = a.RatingCount > 0 ? System.Math.Round(a.RatingSum / a.RatingCount, 2) : (double?)null
                }).ToList();

                _cache.Set(cacheKey, result, TimeSpan.FromHours(12));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SPORTS] team players fetch failed for team {TeamId}", teamId);
                return new List<SportsPlayerSeasonStat>();
            }
        }

        public async Task<List<SportsTeamInjury>> GetTeamInjuriesAsync(
            string teamExternalId, int season, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return new List<SportsTeamInjury>();
            if (!int.TryParse(teamExternalId, out var teamId) || teamId <= 0) return new List<SportsTeamInjury>();

            var cacheKey = $"teaminjuries:{teamId}:{season}";
            if (_cache.TryGetValue(cacheKey, out List<SportsTeamInjury>? cached) && cached != null)
            {
                _metrics.RecordCacheHit();
                return cached;
            }

            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballInjuriesTeamResponse>(
                    $"injuries?team={teamId}&season={season}", ct);

                // En güncel kayıt kazanır (fixture tarihi). Oyuncu bazında dedup.
                var latest = new Dictionary<int, (DateTime date, SportsTeamInjury inj)>();
                foreach (var e in resp?.Response ?? new())
                {
                    var pid = e.Player?.Id ?? 0;
                    if (pid == 0 || string.IsNullOrWhiteSpace(e.Player?.Name)) continue;
                    DateTime.TryParse(e.Fixture?.Date, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var d);
                    if (latest.TryGetValue(pid, out var cur) && cur.date >= d) continue;
                    latest[pid] = (d, new SportsTeamInjury
                    {
                        PlayerId = pid, Name = e.Player!.Name!,
                        Type = e.Player.Type ?? string.Empty, Reason = e.Player.Reason ?? string.Empty
                    });
                }

                var result = latest.Values.Select(v => v.inj).ToList();
                _cache.Set(cacheKey, result, TimeSpan.FromHours(6));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SPORTS] team injuries fetch failed for team {TeamId}", teamId);
                return new List<SportsTeamInjury>();
            }
        }

        private sealed class PlayerAcc
        {
            public int Id; public string Name = ""; public string Position = "";
            public int Goals, Assists, Minutes, Apps, Yellow, Red, RatingCount, Shots, KeyPasses;
            public double RatingSum;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Real Market Odds — GET /odds?date=&page= (gün bazlı sayfalı)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Market oranlarını hangi bahis sağlayıcısından okuyacağımızın tercih sırası.
        /// Sıra likidite/kapsam içindir: listede ÖNDE olan bir sağlayıcı marketi veriyorsa o
        /// kullanılır. Hiçbiri vermiyorsa yanıttaki İLK sağlayıcı kullanılır — böylece market
        /// yalnız gerçekten hiç oran yoksa boş kalır.
        /// </summary>
        private static readonly int[] PreferredBookmakerIds = { 8, 6, 2, 4, 1 };

        // Sağlayıcı bet id'leri (api-football sabitleri).
        private const int BetMatchWinner    = 1;
        private const int BetGoalsOverUnder = 5;
        private const int BetGoalsOuHalf    = 6;
        private const int BetBtts           = 8;
        private const int BetDoubleChance   = 12;
        private const int BetFirstHalfWin   = 13;
        private const int BetTeamScoreFirst = 14;

        public async Task<SportsOddsPage> GetOddsByDateAsync(
            DateTime date, int page, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return new SportsOddsPage();

            var dateKey = date.ToString("yyyy-MM-dd");
            var cacheKey = $"odds:date:{dateKey}:{page}";
            if (_cache.TryGetValue(cacheKey, out SportsOddsPage? cached) && cached != null)
            {
                _metrics.RecordCacheHit();
                return cached;
            }

            try
            {
                var resp = await _http.GetFromJsonAsync<ApiFootballOddsResponse>(
                    $"odds?date={dateKey}&page={page}", ct);

                var result = new SportsOddsPage
                {
                    TotalPages = resp?.Paging?.Total ?? 0,
                    Items = new List<SportsFixtureOdds>()
                };

                foreach (var item in resp?.Response ?? new List<ApiFootballOddsItem>())
                {
                    var fixtureId = item.Fixture?.Id;
                    if (fixtureId == null || fixtureId <= 0) continue;

                    var markets = NormaliseBookmakerOdds(item.Bookmakers);
                    if (markets.Count == 0) continue;

                    result.Items.Add(new SportsFixtureOdds
                    {
                        FixtureExternalId = fixtureId.Value.ToString(),
                        Markets = markets
                    });
                }

                // TTL: pre-match oranlar dakikalar mertebesinde oynar; 10 dk hem tazelik hem kota.
                // BOŞ sayfa CACHE'LENMEZ: api-football dakikalık limit aşımında HTTP 200 +
                // errors.rateLimit döner (gövde boş). Bunu 10 dk saklamak geçici bir limiti
                // kalıcı "oran yok"a çevirirdi — bir sonraki tur yeniden denesin.
                if (result.Items.Count > 0)
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SPORTS] /odds fetch failed for {Date} page {Page}", dateKey, page);
                return new SportsOddsPage();
            }
        }

        /// <summary>
        /// Sağlayıcının bet/value adlarını normalize market anahtarlarına çevirir.
        /// Her market için tercih sırasında EN ÖNDEKİ sağlayıcının oranı alınır; eşlenemeyen
        /// bet/value çiftleri sessizce atlanır (uydurma yok).
        /// </summary>
        private static Dictionary<string, SportsMarketOdd> NormaliseBookmakerOdds(
            List<ApiFootballOddsBookmaker>? bookmakers)
        {
            var markets = new Dictionary<string, SportsMarketOdd>();
            if (bookmakers == null || bookmakers.Count == 0) return markets;

            var ordered = bookmakers
                .OrderBy(b =>
                {
                    var idx = Array.IndexOf(PreferredBookmakerIds, b.Id ?? -1);
                    return idx >= 0 ? idx : PreferredBookmakerIds.Length;
                })
                .ToList();

            foreach (var bm in ordered)
            {
                var bmId = bm.Id ?? 0;
                var bmName = bm.Name ?? string.Empty;

                foreach (var bet in bm.Bets ?? new List<ApiFootballOddsBet>())
                {
                    foreach (var v in bet.Values ?? new List<ApiFootballOddsValue>())
                    {
                        var key = MapMarketKey(bet.Id ?? 0, v.Value);
                        if (key == null) continue;
                        if (markets.ContainsKey(key)) continue;               // tercih sırası kazanır
                        if (!decimal.TryParse(v.Odd,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var odd)) continue;
                        if (odd <= 1m) continue;                              // geçersiz oran

                        markets[key] = new SportsMarketOdd
                        {
                            Odd = odd,
                            BookmakerId = bmId,
                            BookmakerName = bmName
                        };
                    }
                }
            }

            return markets;
        }

        /// <summary>(bet id, value) → normalize market anahtarı. Eşleşme yoksa null.</summary>
        private static string? MapMarketKey(int betId, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim();

            return betId switch
            {
                BetMatchWinner => v switch
                {
                    "Home" => OddsMarketKeys.Ms1,
                    "Draw" => OddsMarketKeys.MsX,
                    "Away" => OddsMarketKeys.Ms2,
                    _ => null
                },
                BetDoubleChance => v switch
                {
                    "Home/Draw" => OddsMarketKeys.DoubleChance1X,
                    "Home/Away" => OddsMarketKeys.DoubleChance12,
                    "Draw/Away" => OddsMarketKeys.DoubleChanceX2,
                    _ => null
                },
                BetGoalsOverUnder => v switch
                {
                    "Over 2.5"  => OddsMarketKeys.Over25,
                    "Under 2.5" => OddsMarketKeys.Under25,
                    _ => null
                },
                BetBtts => v switch
                {
                    "Yes" => OddsMarketKeys.BttsYes,
                    "No"  => OddsMarketKeys.BttsNo,
                    _ => null
                },
                BetFirstHalfWin => v switch
                {
                    "Home" => OddsMarketKeys.Ht1,
                    "Draw" => OddsMarketKeys.HtX,
                    "Away" => OddsMarketKeys.Ht2,
                    _ => null
                },
                BetGoalsOuHalf => v == "Over 0.5" ? OddsMarketKeys.HtOver05 : null,
                BetTeamScoreFirst => v switch
                {
                    "Home" => OddsMarketKeys.FirstGoalHome,
                    "Away" => OddsMarketKeys.FirstGoalAway,
                    _ => null
                },
                _ => null
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Momentum derivation helper (shared with job)
        // ──────────────────────────────────────────────────────────────────────

        internal static SportsLiveMomentum DeriveMomentum(SportsLiveStats stats, int minute)
        {
            var totalDa = stats.DangerousAttacksHome + stats.DangerousAttacksAway;
            int homePressure, awayPressure;

            if (totalDa > 0)
            {
                homePressure = (int)Math.Round((double)stats.DangerousAttacksHome / totalDa * 100);
                awayPressure = 100 - homePressure;
            }
            else if (stats.PossessionHome > 0)
            {
                homePressure = stats.PossessionHome;
                awayPressure = stats.PossessionAway;
            }
            else
            {
                homePressure = 50;
                awayPressure = 50;
            }

            return new SportsLiveMomentum
            {
                HomePressure = Math.Clamp(homePressure, 0, 100),
                AwayPressure = Math.Clamp(awayPressure, 0, 100),
                MinuteBucket = minute
            };
        }

        private static string NormaliseEventType(string? raw)
        {
            return raw?.ToLowerInvariant() switch
            {
                "goal" => "Goal",
                "card" => "Card",
                "subst" => "Substitution",
                "var" => "Var",
                _ => "Other"
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // api-football JSON models
        // ──────────────────────────────────────────────────────────────────────

        // ── Status JSON models (/status) ───────────────────────────────────────
        private class ApiFootballStatusResponse
        {
            [JsonPropertyName("response")]
            public ApiFootballStatusBody? Response { get; set; }
        }

        private class ApiFootballStatusBody
        {
            [JsonPropertyName("requests")]
            public ApiFootballStatusRequests? Requests { get; set; }

            [JsonPropertyName("subscription")]
            public ApiFootballStatusSubscription? Subscription { get; set; }
        }

        private class ApiFootballStatusRequests
        {
            [JsonPropertyName("current")]
            public int? Current { get; set; }

            [JsonPropertyName("limit_day")]
            public int? LimitDay { get; set; }
        }

        private class ApiFootballStatusSubscription
        {
            [JsonPropertyName("plan")]
            public string? Plan { get; set; }
        }

        // ── Football Intelligence: /players?team=&season= models ────────────────
        private class ApiFootballPlayersResponse
        {
            [JsonPropertyName("paging")] public ApiFootballPaging? Paging { get; set; }
            [JsonPropertyName("response")] public List<ApiFootballPlayerItem> Response { get; set; } = new();
        }
        private class ApiFootballPaging
        {
            [JsonPropertyName("current")] public int? Current { get; set; }
            [JsonPropertyName("total")] public int? Total { get; set; }
        }

        // ── Real Market Odds: /odds?date=&page= models ──────────────────────────
        private class ApiFootballOddsResponse
        {
            [JsonPropertyName("paging")] public ApiFootballPaging? Paging { get; set; }
            [JsonPropertyName("response")] public List<ApiFootballOddsItem> Response { get; set; } = new();
        }
        private class ApiFootballOddsItem
        {
            [JsonPropertyName("fixture")] public ApiFootballOddsFixture? Fixture { get; set; }
            [JsonPropertyName("bookmakers")] public List<ApiFootballOddsBookmaker> Bookmakers { get; set; } = new();
        }
        private class ApiFootballOddsFixture
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
        }
        private class ApiFootballOddsBookmaker
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("bets")] public List<ApiFootballOddsBet> Bets { get; set; } = new();
        }
        private class ApiFootballOddsBet
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("values")] public List<ApiFootballOddsValue> Values { get; set; } = new();
        }
        /// <summary>
        /// DİKKAT: api-football bu iki alanı marketten markete FARKLI tiplerde döner —
        /// çoğu markette string ("Home", "Over 2.5", "1.83"), bazılarında ham sayı (ör. handikap
        /// çizgileri, "Total - Home" satırları). Alanları string olarak tiplersek TÜM sayfa
        /// deserialize hatasıyla düşer ve HİÇ oran gelmez. Bu yüzden JsonElement okunup metne
        /// çevrilir (dönüşüm yalnız biçimseldir; değer değiştirilmez).
        /// </summary>
        private class ApiFootballOddsValue
        {
            [JsonPropertyName("value")] public System.Text.Json.JsonElement? ValueRaw { get; set; }
            [JsonPropertyName("odd")] public System.Text.Json.JsonElement? OddRaw { get; set; }

            // [JsonIgnore] ZORUNLU: aksi halde bu türetilmiş üyeler de "value"/"odd" adına
            // eşlenip ham alanlarla çakışır ve deserializer türü hiç kuramaz.
            [JsonIgnore] public string? Value => AsText(ValueRaw);
            [JsonIgnore] public string? Odd => AsText(OddRaw);

            private static string? AsText(System.Text.Json.JsonElement? e)
            {
                if (e == null) return null;
                var el = e.Value;
                return el.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => el.GetString(),
                    System.Text.Json.JsonValueKind.Number => el.GetRawText(),
                    _ => null
                };
            }
        }
        private class ApiFootballPlayerItem
        {
            [JsonPropertyName("player")] public ApiFootballPlayerCore? Player { get; set; }
            [JsonPropertyName("statistics")] public List<ApiFootballPlayerStatLine>? Statistics { get; set; }
        }
        private class ApiFootballPlayerCore
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
        }
        private class ApiFootballPlayerStatLine
        {
            [JsonPropertyName("games")] public ApiFootballPlayerGames? Games { get; set; }
            [JsonPropertyName("goals")] public ApiFootballPlayerGoals? Goals { get; set; }
            [JsonPropertyName("cards")] public ApiFootballPlayerCards? Cards { get; set; }
            [JsonPropertyName("shots")] public ApiFootballPlayerShots? Shots { get; set; }
            [JsonPropertyName("passes")] public ApiFootballPlayerPasses? Passes { get; set; }
        }
        private class ApiFootballPlayerShots
        {
            [JsonPropertyName("total")] public int? Total { get; set; }
        }
        private class ApiFootballPlayerPasses
        {
            [JsonPropertyName("key")] public int? Key { get; set; }
        }
        private class ApiFootballPlayerGames
        {
            [JsonPropertyName("appearences")] public int? Appearences { get; set; }
            [JsonPropertyName("minutes")] public int? Minutes { get; set; }
            [JsonPropertyName("position")] public string? Position { get; set; }
            [JsonPropertyName("rating")] public string? Rating { get; set; }
        }
        private class ApiFootballPlayerGoals
        {
            [JsonPropertyName("total")] public int? Total { get; set; }
            [JsonPropertyName("assists")] public int? Assists { get; set; }
        }
        private class ApiFootballPlayerCards
        {
            [JsonPropertyName("yellow")] public int? Yellow { get; set; }
            [JsonPropertyName("red")] public int? Red { get; set; }
        }

        // ── Football Intelligence: /injuries?team=&season= models ───────────────
        private class ApiFootballInjuriesTeamResponse
        {
            [JsonPropertyName("response")] public List<ApiFootballInjuryTeamItem> Response { get; set; } = new();
        }
        private class ApiFootballInjuryTeamItem
        {
            [JsonPropertyName("player")] public ApiFootballInjuryTeamPlayer? Player { get; set; }
            [JsonPropertyName("fixture")] public ApiFootballInjuryFixture? Fixture { get; set; }
        }
        private class ApiFootballInjuryTeamPlayer
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("type")] public string? Type { get; set; }
            [JsonPropertyName("reason")] public string? Reason { get; set; }
        }
        private class ApiFootballInjuryFixture
        {
            [JsonPropertyName("date")] public string? Date { get; set; }
        }

        private class ApiFootballLineupResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballTeamLineup> Response { get; set; } = new();
        }

        private class ApiFootballTeamLineup
        {
            /// <summary>Açıklanan diziliş — "4-4-2", "4-2-3-1"… Sağlayıcı veriyor, TAHMİN EDİLMEZ.</summary>
            [JsonPropertyName("formation")]
            public string? Formation { get; set; }

            [JsonPropertyName("startXI")]
            public List<ApiFootballPlayerEntry> StartXI { get; set; } = new();

            [JsonPropertyName("substitutes")]
            public List<ApiFootballPlayerEntry> Substitutes { get; set; } = new();
        }

        private class ApiFootballPlayerEntry
        {
            [JsonPropertyName("player")]
            public ApiFootballPlayerInfo? Player { get; set; }
        }

        private class ApiFootballPlayerInfo
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("number")]
            public int? Number { get; set; }

            [JsonPropertyName("pos")]
            public string? Pos { get; set; }

            /// <summary>
            /// Sağlayıcının verdiği saha koordinatı: "hat:sıra" (ör. "1:1" kaleci,
            /// "2:4" savunmanın 4. oyuncusu). Gerçek diziliş bundan çizilir; frontend
            /// oyuncuyu kendi kafasına göre dağıtmaz.
            /// </summary>
            [JsonPropertyName("grid")]
            public string? Grid { get; set; }

            [JsonPropertyName("captain")]
            public string? Captain { get; set; }
        }

        private class ApiFootballInjuryResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballInjuryEntry> Response { get; set; } = new();
        }

        private class ApiFootballInjuryEntry
        {
            [JsonPropertyName("player")]
            public ApiFootballInjuredPlayer? Player { get; set; }

            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }
        }

        private class ApiFootballInjuredPlayer
        {
            // id + type api-football yanıtında ZATEN vardır (bkz. ApiFootballInjuryTeamPlayer,
            // aynı /injuries ailesi). Fixture-scoped okumada deserialize EDİLMİYORDU:
            //   • id   → oyuncu kimliği kayboluyordu (tekilleştirme yapılamıyordu),
            //   • type → "Missing Fixture" / "Questionable" ayrımı kayboluyordu.
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("reason")]
            public string? Reason { get; set; }
        }

        private class ApiFootballTeamRef
        {
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        // ── Standings JSON models ──────────────────────────────────────────────

        private class ApiFootballStandingsResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballStandingsEntry> Response { get; set; } = new();
        }

        private class ApiFootballStandingsEntry
        {
            [JsonPropertyName("league")]
            public ApiFootballStandingsLeague? League { get; set; }
        }

        private class ApiFootballStandingsLeague
        {
            [JsonPropertyName("standings")]
            public List<List<ApiFootballStandingRow>> Standings { get; set; } = new();
        }

        private class ApiFootballStandingRow
        {
            [JsonPropertyName("rank")]
            public int Rank { get; set; }

            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }

            /// <summary>
            /// NULLABLE OLMAK ZORUNDA. Ölçüldü (14.08): api-football Championship (lig 40,
            /// sezon 2026) tablosunda 24. sıradaki takımın (Southampton) "points" alanı NULL
            /// geliyor. int olarak okunduğunda JsonException atılıyor ve LİGİN TÜM tablosu
            /// (24 satır) kayboluyordu — Championship puan durumu bu yüzden hiç gelmedi.
            /// Değer yoksa 0 kabul edilir; başka lig/satır etkilenmez.
            /// </summary>
            [JsonPropertyName("points")]
            public int? Points { get; set; }

            [JsonPropertyName("form")]
            public string? Form { get; set; }

            [JsonPropertyName("all")]
            public ApiFootballAllStats? All { get; set; }
        }

        private class ApiFootballAllStats
        {
            [JsonPropertyName("played")]
            public int Played { get; set; }

            [JsonPropertyName("win")]
            public int Win { get; set; }

            [JsonPropertyName("draw")]
            public int Draw { get; set; }

            [JsonPropertyName("lose")]
            public int Lose { get; set; }

            [JsonPropertyName("goals")]
            public ApiFootballGoalStats? Goals { get; set; }
        }

        private class ApiFootballGoalStats
        {
            [JsonPropertyName("for")]
            public int For { get; set; }

            [JsonPropertyName("against")]
            public int Against { get; set; }
        }

        // ── Phase 6: Team season statistics JSON models (/teams/statistics) ─────

        private class ApiFootballTeamStatsResponse
        {
            [JsonPropertyName("response")]
            public ApiFootballTeamStatsBody? Response { get; set; }
        }

        private class ApiFootballTeamStatsBody
        {
            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }

            [JsonPropertyName("form")]
            public string? Form { get; set; }

            [JsonPropertyName("fixtures")]
            public ApiFootballTsFixtures? Fixtures { get; set; }

            [JsonPropertyName("goals")]
            public ApiFootballTsGoals? Goals { get; set; }

            [JsonPropertyName("clean_sheet")]
            public ApiFootballTsHomeAwayTotal? CleanSheet { get; set; }

            [JsonPropertyName("failed_to_score")]
            public ApiFootballTsHomeAwayTotal? FailedToScore { get; set; }
        }

        private class ApiFootballTsFixtures
        {
            [JsonPropertyName("played")]
            public ApiFootballTsHomeAwayTotal? Played { get; set; }

            [JsonPropertyName("wins")]
            public ApiFootballTsHomeAwayTotal? Wins { get; set; }

            [JsonPropertyName("draws")]
            public ApiFootballTsHomeAwayTotal? Draws { get; set; }

            [JsonPropertyName("loses")]
            public ApiFootballTsHomeAwayTotal? Loses { get; set; }
        }

        private class ApiFootballTsHomeAwayTotal
        {
            [JsonPropertyName("home")]
            public int? Home { get; set; }

            [JsonPropertyName("away")]
            public int? Away { get; set; }

            [JsonPropertyName("total")]
            public int? Total { get; set; }
        }

        private class ApiFootballTsGoals
        {
            [JsonPropertyName("for")]
            public ApiFootballTsGoalSide? For { get; set; }

            [JsonPropertyName("against")]
            public ApiFootballTsGoalSide? Against { get; set; }
        }

        private class ApiFootballTsGoalSide
        {
            [JsonPropertyName("average")]
            public ApiFootballTsAverage? Average { get; set; }
        }

        private class ApiFootballTsAverage
        {
            /// <summary>Average values arrive as strings ("1.8").</summary>
            [JsonPropertyName("home")]
            public string? Home { get; set; }

            [JsonPropertyName("away")]
            public string? Away { get; set; }

            [JsonPropertyName("total")]
            public string? Total { get; set; }
        }

        // ── Phase 6 / Slice 2: Prediction JSON models (/predictions) ────────────

        private class ApiFootballPredictionResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballPredictionBody> Response { get; set; } = new();
        }

        private class ApiFootballPredictionBody
        {
            [JsonPropertyName("predictions")]
            public ApiFootballPredictionBlock? Predictions { get; set; }

            [JsonPropertyName("teams")]
            public ApiFootballFixtureSyncTeams? Teams { get; set; }

            [JsonPropertyName("comparison")]
            public ApiFootballPredComparison? Comparison { get; set; }
        }

        private class ApiFootballPredictionBlock
        {
            [JsonPropertyName("winner")]
            public ApiFootballPredWinner? Winner { get; set; }

            [JsonPropertyName("win_or_draw")]
            public bool? WinOrDraw { get; set; }

            [JsonPropertyName("under_over")]
            public string? UnderOver { get; set; }

            [JsonPropertyName("advice")]
            public string? Advice { get; set; }

            [JsonPropertyName("percent")]
            public ApiFootballPredPercent? Percent { get; set; }
        }

        private class ApiFootballPredWinner
        {
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private class ApiFootballPredPercent
        {
            [JsonPropertyName("home")]
            public string? Home { get; set; }

            [JsonPropertyName("draw")]
            public string? Draw { get; set; }

            [JsonPropertyName("away")]
            public string? Away { get; set; }
        }

        private class ApiFootballPredComparison
        {
            [JsonPropertyName("total")]
            public ApiFootballPredHomeAway? Total { get; set; }
        }

        private class ApiFootballPredHomeAway
        {
            [JsonPropertyName("home")]
            public string? Home { get; set; }

            [JsonPropertyName("away")]
            public string? Away { get; set; }
        }

        // ── Phase 6 Final: Team profile JSON models ─────────────────────────────

        private class ApiFootballCoachResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballCoachItem> Response { get; set; } = new();
        }

        private class ApiFootballCoachItem
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("age")]
            public int? Age { get; set; }

            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }
        }

        private class ApiFootballTeamsInfoResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballTeamInfoItem> Response { get; set; } = new();
        }

        private class ApiFootballTeamInfoItem
        {
            [JsonPropertyName("venue")]
            public ApiFootballVenueBlock? Venue { get; set; }
        }

        private class ApiFootballVenueBlock
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("city")]
            public string? City { get; set; }

            [JsonPropertyName("capacity")]
            public int? Capacity { get; set; }

            [JsonPropertyName("surface")]
            public string? Surface { get; set; }
        }

        private class ApiFootballSquadResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballSquadItem> Response { get; set; } = new();
        }

        private class ApiFootballSquadItem
        {
            [JsonPropertyName("players")]
            public List<ApiFootballSquadPlayer> Players { get; set; } = new();
        }

        private class ApiFootballSquadPlayer
        {
            [JsonPropertyName("age")]
            public int? Age { get; set; }
        }

        private class ApiFootballTransfersResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballTransferItem> Response { get; set; } = new();
        }

        private class ApiFootballTransferItem
        {
            [JsonPropertyName("transfers")]
            public List<ApiFootballTransferMove> Transfers { get; set; } = new();
        }

        private class ApiFootballTransferMove
        {
            [JsonPropertyName("date")]
            public string? Date { get; set; }

            [JsonPropertyName("teams")]
            public ApiFootballTransferTeams? Teams { get; set; }
        }

        private class ApiFootballTransferTeams
        {
            [JsonPropertyName("in")]
            public ApiFootballTeamRef? In { get; set; }

            [JsonPropertyName("out")]
            public ApiFootballTeamRef? Out { get; set; }
        }

        // ── Fixture / competition context JSON models ──────────────────────────

        private class ApiFootballFixtureResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballFixtureEntry> Response { get; set; } = new();
        }

        private class ApiFootballFixtureEntry
        {
            [JsonPropertyName("league")]
            public ApiFootballFixtureLeague? League { get; set; }
        }

        private class ApiFootballFixtureLeague
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("round")]
            public string? Round { get; set; }
        }

        // ── Live fixture JSON models ───────────────────────────────────────────

        private class ApiFootballLiveFixtureResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballLiveFixtureEntry> Response { get; set; } = new();
        }

        private class ApiFootballLiveFixtureEntry
        {
            [JsonPropertyName("fixture")]
            public ApiFootballLiveFixtureInfo? Fixture { get; set; }

            [JsonPropertyName("goals")]
            public ApiFootballGoalsEntry? Goals { get; set; }
        }

        private class ApiFootballLiveFixtureInfo
        {
            /// <summary>api-football fixture id — present in all fixture responses.</summary>
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("status")]
            public ApiFootballFixtureStatus? Status { get; set; }
        }

        private class ApiFootballFixtureStatus
        {
            [JsonPropertyName("short")]
            public string? Short { get; set; }

            [JsonPropertyName("elapsed")]
            public int? Elapsed { get; set; }
        }

        private class ApiFootballGoalsEntry
        {
            [JsonPropertyName("home")]
            public int? Home { get; set; }

            [JsonPropertyName("away")]
            public int? Away { get; set; }
        }

        // ── Live statistics JSON models ────────────────────────────────────────

        private class ApiFootballStatisticsResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballTeamStats> Response { get; set; } = new();
        }

        private class ApiFootballTeamStats
        {
            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }

            [JsonPropertyName("statistics")]
            public List<ApiFootballStatEntry> Statistics { get; set; } = new();
        }

        private class ApiFootballStatEntry
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            /// <summary>Value may be int, string ("58%", "1.20"), or null.</summary>
            [JsonPropertyName("value")]
            public object? Value { get; set; }
        }

        // ── Sprint 0: Fixture sync JSON models ────────────────────────────────
        // Full fixture entry: fixture meta + league + teams
        // Endpoint: GET /fixtures?from=YYYY-MM-DD&to=YYYY-MM-DD

        private class ApiFootballFixtureSyncResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballFixtureSyncEntry> Response { get; set; } = new();
        }

        private class ApiFootballFixtureSyncEntry
        {
            [JsonPropertyName("fixture")]
            public ApiFootballFixtureSyncInfo? Fixture { get; set; }

            [JsonPropertyName("league")]
            public ApiFootballFixtureSyncLeague? League { get; set; }

            [JsonPropertyName("teams")]
            public ApiFootballFixtureSyncTeams? Teams { get; set; }

            [JsonPropertyName("goals")]
            public ApiFootballGoalsEntry? Goals { get; set; }

            /// <summary>score.halftime / score.fulltime — İLK YARI buradan gelir.</summary>
            [JsonPropertyName("score")]
            public ApiFootballScoreEntry? Score { get; set; }
        }

        /// <summary>Sağlayıcının skor kırılımı; şu an yalnız halftime okunur.</summary>
        private class ApiFootballScoreEntry
        {
            [JsonPropertyName("halftime")]
            public ApiFootballGoalsEntry? Halftime { get; set; }
        }

        private class ApiFootballFixtureSyncInfo
        {
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            /// <summary>ISO 8601 date string, e.g. "2024-01-15T18:00:00+00:00".</summary>
            [JsonPropertyName("date")]
            public string? Date { get; set; }

            [JsonPropertyName("referee")]
            public string? Referee { get; set; }

            [JsonPropertyName("venue")]
            public ApiFootballFixtureSyncVenue? Venue { get; set; }

            [JsonPropertyName("status")]
            public ApiFootballFixtureStatus? Status { get; set; }
        }

        private class ApiFootballFixtureSyncVenue
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("city")]
            public string? City { get; set; }
        }

        private class ApiFootballFixtureSyncLeague
        {
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("round")]
            public string? Round { get; set; }
        }

        private class ApiFootballFixtureSyncTeams
        {
            [JsonPropertyName("home")]
            public ApiFootballFixtureSyncTeamInfo? Home { get; set; }

            [JsonPropertyName("away")]
            public ApiFootballFixtureSyncTeamInfo? Away { get; set; }
        }

        private class ApiFootballFixtureSyncTeamInfo
        {
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("logo")]
            public string? Logo { get; set; }
        }

        // ── Live events JSON models ────────────────────────────────────────────

        private class ApiFootballEventsResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballEventEntry> Response { get; set; } = new();
        }

        private class ApiFootballEventEntry
        {
            [JsonPropertyName("time")]
            public ApiFootballEventTime? Time { get; set; }

            [JsonPropertyName("team")]
            public ApiFootballTeamRef? Team { get; set; }

            [JsonPropertyName("player")]
            public ApiFootballPersonRef? Player { get; set; }

            [JsonPropertyName("assist")]
            public ApiFootballPersonRef? Assist { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("detail")]
            public string? Detail { get; set; }
        }

        private class ApiFootballEventTime
        {
            [JsonPropertyName("elapsed")]
            public int? Elapsed { get; set; }

            /// <summary>Uzatma dakikası (ör. 90+<b>4</b>). null = uzatma yok.</summary>
            [JsonPropertyName("extra")]
            public int? Extra { get; set; }
        }

        private class ApiFootballPersonRef
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
