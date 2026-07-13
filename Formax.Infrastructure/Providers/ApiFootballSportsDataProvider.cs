using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Providers
{
    /// <summary>
    /// Sports data provider backed by api-football.com (v3).
    /// Config keys: SportsData:ApiKey, SportsData:BaseUrl (optional).
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
        private readonly string _apiKey;

        // ── Cache TTLs ────────────────────────────────────────────────────────
        private static readonly TimeSpan TtlFixtures    = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlLineup      = TimeSpan.FromHours(1);
        private static readonly TimeSpan TtlInjuries    = TimeSpan.FromHours(4);
        private static readonly TimeSpan TtlStandings   = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlCompCtx     = TimeSpan.FromHours(24);
        private static readonly TimeSpan TtlLive        = TimeSpan.FromSeconds(60);

        public ApiFootballSportsDataProvider(
            HttpClient http,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<ApiFootballSportsDataProvider> logger)
        {
            _http   = http;
            _cache  = cache;
            _logger = logger;

            _apiKey = configuration["SportsData:ApiKey"] ?? string.Empty;

            var baseUrl = configuration["SportsData:BaseUrl"] ?? DefaultBaseUrl;
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Add("x-apisports-key", _apiKey);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Sprint 0: Fixture sync
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calls GET /fixtures?from=YYYY-MM-DD&amp;to=YYYY-MM-DD — one request per cycle.
        /// Returns all fixtures in the window regardless of status.
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

                var response = await _http.GetFromJsonAsync<ApiFootballFixtureSyncResponse>(
                    $"fixtures?from={from}&to={to}", ct);

                if (response?.Response == null)
                    return new List<SportsFixtureResult>();

                var results = new List<SportsFixtureResult>();

                foreach (var entry in response.Response)
                {
                    var fixtureId = entry.Fixture?.Id;
                    if (fixtureId == null) continue;

                    var rawDate = entry.Fixture?.Date;
                    if (!DateTime.TryParse(rawDate, null,
                            System.Globalization.DateTimeStyles.RoundtripKind,
                            out var matchDate))
                        continue;

                    results.Add(new SportsFixtureResult
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
                        AwayScore          = entry.Goals?.Away
                    });
                }

                _logger.LogDebug(
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
        /// Team recent results — interface compliance. Not used by the active
        /// historical sync (TheSportsDB is the configured provider). Returns empty.
        /// </summary>
        public Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
            string externalTeamId, CancellationToken ct = default)
            => Task.FromResult(new List<SportsFixtureResult>());

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
                return cachedLineup;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballLineupResponse>(
                    $"fixtures/lineups?fixture={matchExternalId}",
                    ct);

                if (response?.Response == null || response.Response.Count == 0)
                    return null;

                // Two team entries: index 0 = home, index 1 = away (per api-football spec)
                var home = response.Response.ElementAtOrDefault(0);
                var away = response.Response.ElementAtOrDefault(1);

                if (home == null && away == null)
                    return null;

                var result = new SportsLineupResult
                {
                    LineupsAnnounced = home?.StartXI?.Count > 0 || away?.StartXI?.Count > 0,
                    HomeStarters = MapPlayers(home?.StartXI),
                    HomeBench = MapPlayers(home?.Substitutes),
                    AwayStarters = MapPlayers(away?.StartXI),
                    AwayBench = MapPlayers(away?.Substitutes)
                };

                _cache.Set(cacheKey, result, TtlLineup);
                return result;
            }
            catch (Exception ex)
            {
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
                return cachedStatuses;

            try
            {
                var response = await _http.GetFromJsonAsync<ApiFootballInjuryResponse>(
                    $"injuries?fixture={matchExternalId}",
                    ct);

                if (response?.Response == null)
                    return new List<SportsPlayerStatusResult>();

                var result = response.Response
                    .Select(r => new SportsPlayerStatusResult
                    {
                        PlayerName = r.Player?.Name ?? string.Empty,
                        TeamId = r.Team?.Id ?? 0,
                        Status = NormaliseStatus(r.Player?.Reason),
                        Reason = r.Player?.Reason ?? string.Empty
                    })
                    .Where(r => !string.IsNullOrWhiteSpace(r.PlayerName))
                    .ToList();

                _cache.Set(cacheKey, result, TtlInjuries);
                return result;
            }
            catch (Exception ex)
            {
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
                IsCaptain = e.Player?.Captain == "1" || e.Player?.Captain == "true"
            }).ToList();
        }

        private static string NormaliseStatus(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Doubtful";

            var lower = raw.ToLowerInvariant();
            if (lower.Contains("suspend")) return "Suspended";
            if (lower.Contains("injur") || lower.Contains("miss")) return "Injured";
            return "Doubtful";
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
                    Points = r.Points,
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
                    $"fixtures?id={matchExternalId}",
                    ct);

                var fixture = response?.Response?.FirstOrDefault();
                if (fixture?.League == null)
                    return null;

                var compType = NormaliseCompetitionType(fixture.League.Type);
                var stageName = fixture.League.Round ?? string.Empty;

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

        private static string NormaliseCompetitionType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "League";
            var lower = raw.ToLowerInvariant();
            if (lower == "cup") return "Cup";
            return "League";
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
                    "fixtures?live=all", ct);

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
                        $"fixtures?id={matchExternalId}", ct);

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
                        Minute = e.Time?.Elapsed ?? 0,
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

        private class ApiFootballLineupResponse
        {
            [JsonPropertyName("response")]
            public List<ApiFootballTeamLineup> Response { get; set; } = new();
        }

        private class ApiFootballTeamLineup
        {
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
            [JsonPropertyName("name")]
            public string? Name { get; set; }

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

            [JsonPropertyName("points")]
            public int Points { get; set; }

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
        }

        private class ApiFootballPersonRef
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
