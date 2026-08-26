using System.Net.Http.Headers;
using System.Text.Json;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Cache;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Providers;

public sealed class ApiFootballH2HProvider : IH2HProvider
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly H2HCache _cache;

    public ApiFootballH2HProvider(IHttpClientFactory http, IConfiguration config, H2HCache cache)
    {
        _http = http;
        _config = config;
        _cache = cache;
    }

    public async Task<H2HDto?> GetAsync(int homeAfId, int awayAfId)
    {
        if (_cache.TryGet(homeAfId, awayAfId, out var cached))
            return cached;

        var apiKey = _config["ApiFootball:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var baseUrl = _config["ApiFootball:BaseUrl"] ?? "https://v3.football.api-sports.io";
        // Configurable timezone — tarih alanları yerel güne göre tutarlı olsun.
        var tz = Formax.Infrastructure.Http.ApiFootballTimeZone.ResolveId(
            _config[Formax.Infrastructure.Http.ApiFootballTimeZone.ConfigKey]);
        // Free plan: last= ve season= opsiyonel; season olmadan tüm geçmiş gelir
        var url = $"{baseUrl}/fixtures/headtohead?h2h={homeAfId}-{awayAfId}&timezone={Uri.EscapeDataString(tz)}";

        try
        {
            using var client = _http.CreateClient("ApiFootball");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var dto = Parse(json, homeAfId);

            if (dto != null)
                _cache.Set(homeAfId, awayAfId, dto);

            return dto;
        }
        catch
        {
            return null;
        }
    }

    private static H2HDto? Parse(string json, int homeAfId)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var fixtures = doc.RootElement.GetProperty("response");

            var matches = new List<H2HMatchDto>();
            int homeWins = 0, awayWins = 0, draws = 0;

            foreach (var fix in fixtures.EnumerateArray())
            {
                var teams = fix.GetProperty("teams");
                var goals = fix.GetProperty("goals");
                var fixtureEl = fix.GetProperty("fixture");
                var league = fix.TryGetProperty("league", out var lg) ? lg.GetProperty("name").GetString() : null;

                var homeTeamId = teams.GetProperty("home").GetProperty("id").GetInt32();
                var homeName = teams.GetProperty("home").GetProperty("name").GetString() ?? "";
                var awayName = teams.GetProperty("away").GetProperty("name").GetString() ?? "";

                var homeGoals = goals.GetProperty("home").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("home").GetInt32();
                var awayGoals = goals.GetProperty("away").ValueKind == JsonValueKind.Null ? 0 : goals.GetProperty("away").GetInt32();

                var dateStr = fixtureEl.GetProperty("date").GetString() ?? "";
                // Frontend string olarak bekliyor; ISO 8601 date prefix yeterli
                var matchDateDisplay = dateStr.Length >= 10 ? dateStr[..10] : dateStr;

                matches.Add(new H2HMatchDto
                {
                    MatchDate = matchDateDisplay,
                    HomeTeamName = homeName,
                    AwayTeamName = awayName,
                    HomeScore = homeGoals,
                    AwayScore = awayGoals,
                    Competition = league
                });

                // kazananı homeAfId perspektifinden hesapla
                bool afTeamIsHome = homeTeamId == homeAfId;
                int afGoals = afTeamIsHome ? homeGoals : awayGoals;
                int oppGoals = afTeamIsHome ? awayGoals : homeGoals;

                if (afGoals > oppGoals) homeWins++;
                else if (afGoals < oppGoals) awayWins++;
                else draws++;
            }

            return new H2HDto
            {
                TotalMatches = matches.Count,
                HomeWins = homeWins,
                AwayWins = awayWins,
                Draws = draws,
                Matches = matches,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }
}
