using System.Globalization;
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
    /// Sports data provider backed by TheSportsDB (https://www.thesportsdb.com).
    ///
    /// Free-tier coverage (incl. Türkiye Süper Lig):
    ///   Fixtures   : GET eventsday.php?d=YYYY-MM-DD&amp;s=Soccer   (per-day, looped over window)
    ///   Standings  : GET lookuptable.php?l={leagueId}&amp;s={season}
    ///   Scores     : intHomeScore / intAwayScore on finished events
    ///
    /// NOT available on the free tier — these return empty/null by design
    /// (ISportsDataProvider contract: never throw, degrade gracefully):
    ///   Lineups, injuries, live statistics, event timeline, momentum.
    ///
    /// Config keys:
    ///   TheSportsDb:ApiKey   (default "123" — public test key)
    ///   TheSportsDb:BaseUrl  (optional)
    ///
    /// Cache TTLs mirror the api-football provider for consistency.
    /// </summary>
    public class TheSportsDbProvider : ISportsDataProvider
    {
        private const string DefaultBaseUrl = "https://www.thesportsdb.com";
        private const string DefaultKey     = "123"; // public test key

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TheSportsDbProvider> _logger;
        private readonly string _apiKey;

        private static readonly TimeSpan TtlFixtures  = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TtlStandings = TimeSpan.FromMinutes(15);
        // Past results change rarely → long TTL keeps the historical sync cheap.
        private static readonly TimeSpan TtlResults   = TimeSpan.FromHours(12);

        public TheSportsDbProvider(
            HttpClient http,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<TheSportsDbProvider> logger)
        {
            _http   = http;
            _cache  = cache;
            _logger = logger;

            _apiKey = string.IsNullOrWhiteSpace(configuration["TheSportsDb:ApiKey"])
                ? DefaultKey
                : configuration["TheSportsDb:ApiKey"]!;

            var baseUrl = configuration["TheSportsDb:BaseUrl"] ?? DefaultBaseUrl;
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        private string Api(string path) => $"api/v1/json/{_apiKey}/{path}";

        // ──────────────────────────────────────────────────────────────────────
        // Fixtures — looped per day over the window (TheSportsDB has no range query)
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsFixtureResult>> GetFixturesAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default)
        {
            var from = fromDate.Date;
            var to   = toDate.Date;
            var cacheKey = $"tsdb:fx:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}";

            if (_cache.TryGetValue(cacheKey, out List<SportsFixtureResult>? cached) && cached != null)
                return cached;

            var results = new List<SportsFixtureResult>();

            try
            {
                // Cap the window to avoid runaway request counts (free-tier safe).
                var days = Math.Min((int)(to - from).TotalDays, 9);
                for (var i = 0; i <= days; i++)
                {
                    var day = from.AddDays(i).ToString("yyyy-MM-dd");
                    var resp = await _http.GetFromJsonAsync<TsdbEventsResponse>(
                        Api($"eventsday.php?d={day}&s=Soccer"), ct);

                    if (resp?.Events == null) continue;

                    foreach (var e in resp.Events)
                    {
                        var mapped = MapEvent(e);
                        if (mapped != null) results.Add(mapped);
                    }
                }

                _logger.LogDebug("[TSDB] GetFixturesAsync: {Count} fixture(s) {From}→{To}",
                    results.Count, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

                _cache.Set(cacheKey, results, TtlFixtures);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TSDB] Fixture fetch failed for window {From}→{To}",
                    from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
                return results; // partial/empty — never throw
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Team recent results — backfill source for HistoricalSyncJob.
        // GET eventslast.php?id={teamId} → last finished matches (scored fixtures).
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
            string externalTeamId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(externalTeamId))
                return new List<SportsFixtureResult>();

            var cacheKey = $"tsdb:last:{externalTeamId}";
            if (_cache.TryGetValue(cacheKey, out List<SportsFixtureResult>? cached) && cached != null)
                return cached;

            try
            {
                var resp = await _http.GetFromJsonAsync<TsdbResultsResponse>(
                    Api($"eventslast.php?id={externalTeamId}"), ct);

                var events = resp?.Results;
                if (events == null || events.Count == 0)
                    return new List<SportsFixtureResult>();

                var results = new List<SportsFixtureResult>();
                foreach (var e in events)
                {
                    var mapped = MapEvent(e);
                    if (mapped != null) results.Add(mapped);
                }

                _logger.LogDebug("[TSDB] GetTeamRecentResultsAsync({TeamId}): {Count} result(s)",
                    externalTeamId, results.Count);

                _cache.Set(cacheKey, results, TtlResults);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TSDB] eventslast fetch failed for team {TeamId}", externalTeamId);
                return new List<SportsFixtureResult>();
            }
        }

        // Shared event → SportsFixtureResult mapping (used by fixtures + results).
        private SportsFixtureResult? MapEvent(TsdbEvent e)
        {
            if (string.IsNullOrWhiteSpace(e.IdEvent)) return null;
            var matchDate = ParseDate(e.DateEvent, e.StrTime);
            if (matchDate == null) return null;

            return new SportsFixtureResult
            {
                ExternalMatchId    = e.IdEvent,
                MatchDate          = matchDate.Value,
                Status             = NormaliseStatus(e.StrStatus),
                LeagueName         = e.StrLeague ?? string.Empty,
                LeagueExternalId   = int.TryParse(e.IdLeague, out var lid) ? lid : 0,
                Round              = e.IntRound ?? string.Empty,
                HomeTeamExternalId = e.IdHomeTeam ?? string.Empty,
                HomeTeamName       = e.StrHomeTeam ?? string.Empty,
                HomeLogoUrl        = e.StrHomeTeamBadge,
                AwayTeamExternalId = e.IdAwayTeam ?? string.Empty,
                AwayTeamName       = e.StrAwayTeam ?? string.Empty,
                AwayLogoUrl        = e.StrAwayTeamBadge,
                Referee            = null,
                Venue              = string.IsNullOrWhiteSpace(e.StrVenue) ? null : e.StrVenue,
                HomeScore          = ParseScore(e.IntHomeScore),
                AwayScore          = ParseScore(e.IntAwayScore)
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Standings
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<SportsStandingEntry>> GetLeagueStandingsAsync(
            string leagueExternalId,
            int season,
            CancellationToken ct = default)
        {
            // TheSportsDB season format: "2025-2026"
            var seasonStr = $"{season}-{season + 1}";
            var cacheKey = $"tsdb:standings:{leagueExternalId}:{seasonStr}";

            if (_cache.TryGetValue(cacheKey, out List<SportsStandingEntry>? cached) && cached != null)
                return cached;

            try
            {
                var resp = await _http.GetFromJsonAsync<TsdbTableResponse>(
                    Api($"lookuptable.php?l={leagueExternalId}&s={seasonStr}"), ct);

                if (resp?.Table == null || resp.Table.Count == 0)
                    return new List<SportsStandingEntry>();

                var result = resp.Table.Select(r => new SportsStandingEntry
                {
                    TeamId       = int.TryParse(r.IdTeam, out var tid) ? tid : 0,
                    TeamName     = r.StrTeam ?? string.Empty,
                    Position     = int.TryParse(r.IntRank, out var rank) ? rank : 0,
                    Played       = ParseInt(r.IntPlayed),
                    Won          = ParseInt(r.IntWin),
                    Drawn        = ParseInt(r.IntDraw),
                    Lost         = ParseInt(r.IntLoss),
                    GoalsFor     = ParseInt(r.IntGoalsFor),
                    GoalsAgainst = ParseInt(r.IntGoalsAgainst),
                    Points       = ParseInt(r.IntPoints),
                    Form         = r.StrForm ?? string.Empty
                })
                .Where(r => r.TeamId > 0)
                .ToList();

                _cache.Set(cacheKey, result, TtlStandings);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TSDB] Standings fetch failed for league {LeagueId} season {Season}",
                    leagueExternalId, seasonStr);
                return new List<SportsStandingEntry>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unsupported on free tier — graceful empty/null (contract: never throw)
        // ──────────────────────────────────────────────────────────────────────

        public Task<SportsLineupResult?> GetOfficialLineupAsync(
            string matchExternalId, CancellationToken ct = default)
            => Task.FromResult<SportsLineupResult?>(null);

        public Task<List<SportsPlayerStatusResult>> GetPlayerStatusesAsync(
            string matchExternalId, CancellationToken ct = default)
            => Task.FromResult(new List<SportsPlayerStatusResult>());

        public Task<SportsCompetitionContext?> GetCompetitionContextAsync(
            string matchExternalId, CancellationToken ct = default)
            => Task.FromResult<SportsCompetitionContext?>(null);

        public Task<List<SportsLiveBatchEntry>> GetAllLiveFixturesAsync(
            CancellationToken ct = default)
            => Task.FromResult(new List<SportsLiveBatchEntry>());

        public Task<SportsLiveStats?> GetLiveMatchStatsAsync(
            string matchExternalId, SportsLiveBatchEntry? batchEntry, CancellationToken ct = default)
            => Task.FromResult<SportsLiveStats?>(null);

        public Task<List<SportsLiveEvent>> GetLiveMatchEventsAsync(
            string matchExternalId, CancellationToken ct = default)
            => Task.FromResult(new List<SportsLiveEvent>());

        public Task<SportsLiveMomentum?> GetLiveMomentumAsync(
            string matchExternalId, CancellationToken ct = default)
            => Task.FromResult<SportsLiveMomentum?>(null);

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static DateTime? ParseDate(string? dateEvent, string? strTime)
        {
            if (string.IsNullOrWhiteSpace(dateEvent)) return null;

            var timePart = string.IsNullOrWhiteSpace(strTime) ? "00:00:00" : strTime;
            // TheSportsDB: dateEvent "2026-03-01", strTime "18:00:00" (UTC)
            if (DateTime.TryParse($"{dateEvent}T{timePart}",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            if (DateTime.TryParse(dateEvent, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dOnly))
                return DateTime.SpecifyKind(dOnly, DateTimeKind.Utc);

            return null;
        }

        private static int? ParseScore(string? raw)
            => int.TryParse(raw, out var v) ? v : (int?)null;

        private static int ParseInt(string? raw)
            => int.TryParse(raw, out var v) ? v : 0;

        /// <summary>
        /// Maps TheSportsDB strStatus to the normalised contract value.
        /// TheSportsDB uses inconsistent strings: "Match Finished", "FT", "NS",
        /// "1H", "2H", "HT", "" (scheduled), sometimes a minute number while live.
        /// </summary>
        private static string NormaliseStatus(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "NotStarted";

            var s = raw.Trim().ToUpperInvariant();
            return s switch
            {
                "FT"   => "Finished",
                "AET"  => "Finished",
                "PEN"  => "Finished",
                "NS"   => "NotStarted",
                "TBD"  => "NotStarted",
                "HT"   => "Live",
                "1H"   => "Live",
                "2H"   => "Live",
                "ET"   => "Live",
                "P"    => "Live",
                "PST"  => "Postponed",
                "CANC" => "Cancelled",
                "ABD"  => "Cancelled",
                _ when s.Contains("FINISH") => "Finished",
                _ when s.Contains("POSTPON") => "Postponed",
                _ when s.Contains("CANCEL") => "Cancelled",
                // A bare minute number ("57") means the match is in play.
                _ when int.TryParse(s, out _) => "Live",
                _ => "NotStarted"
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // TheSportsDB JSON models
        // ──────────────────────────────────────────────────────────────────────

        private class TsdbEventsResponse
        {
            [JsonPropertyName("events")]
            public List<TsdbEvent>? Events { get; set; }
        }

        // eventslast.php returns the array under "results" (not "events").
        private class TsdbResultsResponse
        {
            [JsonPropertyName("results")]
            public List<TsdbEvent>? Results { get; set; }
        }

        private class TsdbEvent
        {
            [JsonPropertyName("idEvent")]      public string? IdEvent { get; set; }
            [JsonPropertyName("strLeague")]    public string? StrLeague { get; set; }
            [JsonPropertyName("idLeague")]     public string? IdLeague { get; set; }
            [JsonPropertyName("intRound")]     public string? IntRound { get; set; }
            [JsonPropertyName("dateEvent")]    public string? DateEvent { get; set; }
            [JsonPropertyName("strTime")]      public string? StrTime { get; set; }
            [JsonPropertyName("strStatus")]    public string? StrStatus { get; set; }
            [JsonPropertyName("strVenue")]     public string? StrVenue { get; set; }

            [JsonPropertyName("idHomeTeam")]       public string? IdHomeTeam { get; set; }
            [JsonPropertyName("strHomeTeam")]      public string? StrHomeTeam { get; set; }
            [JsonPropertyName("strHomeTeamBadge")] public string? StrHomeTeamBadge { get; set; }
            [JsonPropertyName("intHomeScore")]     public string? IntHomeScore { get; set; }

            [JsonPropertyName("idAwayTeam")]       public string? IdAwayTeam { get; set; }
            [JsonPropertyName("strAwayTeam")]      public string? StrAwayTeam { get; set; }
            [JsonPropertyName("strAwayTeamBadge")] public string? StrAwayTeamBadge { get; set; }
            [JsonPropertyName("intAwayScore")]     public string? IntAwayScore { get; set; }
        }

        private class TsdbTableResponse
        {
            [JsonPropertyName("table")]
            public List<TsdbTableRow>? Table { get; set; }
        }

        private class TsdbTableRow
        {
            [JsonPropertyName("idTeam")]        public string? IdTeam { get; set; }
            [JsonPropertyName("strTeam")]       public string? StrTeam { get; set; }
            [JsonPropertyName("intRank")]       public string? IntRank { get; set; }
            [JsonPropertyName("intPlayed")]     public string? IntPlayed { get; set; }
            [JsonPropertyName("intWin")]        public string? IntWin { get; set; }
            [JsonPropertyName("intDraw")]       public string? IntDraw { get; set; }
            [JsonPropertyName("intLoss")]       public string? IntLoss { get; set; }
            [JsonPropertyName("intGoalsFor")]   public string? IntGoalsFor { get; set; }
            [JsonPropertyName("intGoalsAgainst")] public string? IntGoalsAgainst { get; set; }
            [JsonPropertyName("intPoints")]     public string? IntPoints { get; set; }
            [JsonPropertyName("strForm")]       public string? StrForm { get; set; }
        }
    }
}
