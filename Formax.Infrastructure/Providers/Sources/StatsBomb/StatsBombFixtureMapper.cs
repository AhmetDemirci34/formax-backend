using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Providers.Sources.StatsBomb;

/// <summary>
/// StatsBomb ham maç JSON'unu ortak <see cref="RawFixture"/> listesine dönüştürür.
/// SADECE alan eşlemesi: iş kuralı/normalize/isim düzeltmesi YOK; bulunamayan alanlar null.
/// </summary>
public sealed class StatsBombFixtureMapper : IProviderMapper<RawFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderName => "statsbomb";

    public IReadOnlyList<RawFixture> Map(object? payload)
    {
        if (payload is not string json || string.IsNullOrWhiteSpace(json))
            return Array.Empty<RawFixture>();

        List<StatsBombMatch>? matches;
        try { matches = JsonSerializer.Deserialize<List<StatsBombMatch>>(json, JsonOptions); }
        catch (JsonException) { return Array.Empty<RawFixture>(); }

        if (matches is null || matches.Count == 0)
            return Array.Empty<RawFixture>();

        var fixtures = new List<RawFixture>(matches.Count);
        foreach (var match in matches)
        {
            if (match is null)
                continue;

            fixtures.Add(new RawFixture
            {
                ProviderName = ProviderName,
                ProviderMatchId = match.MatchId?.ToString(CultureInfo.InvariantCulture),
                Competition = match.Competition?.CompetitionName,
                Season = match.Season?.SeasonName,
                HomeTeam = match.HomeTeam?.HomeTeamName,
                AwayTeam = match.AwayTeam?.AwayTeamName,
                Kickoff = match.MatchDate,
                Status = match.MatchStatus,
                HomeScore = match.HomeScore,
                AwayScore = match.AwayScore
            });
        }

        return fixtures;
    }

    // ---- StatsBomb ham şeması (snake_case) ----

    private sealed class StatsBombMatch
    {
        [JsonPropertyName("match_id")] public long? MatchId { get; set; }
        [JsonPropertyName("match_date")] public string? MatchDate { get; set; }
        [JsonPropertyName("match_status")] public string? MatchStatus { get; set; }
        [JsonPropertyName("home_score")] public int? HomeScore { get; set; }
        [JsonPropertyName("away_score")] public int? AwayScore { get; set; }
        [JsonPropertyName("home_team")] public StatsBombHomeTeam? HomeTeam { get; set; }
        [JsonPropertyName("away_team")] public StatsBombAwayTeam? AwayTeam { get; set; }
        [JsonPropertyName("competition")] public StatsBombCompetition? Competition { get; set; }
        [JsonPropertyName("season")] public StatsBombSeason? Season { get; set; }
    }

    private sealed class StatsBombHomeTeam
    {
        [JsonPropertyName("home_team_name")] public string? HomeTeamName { get; set; }
    }

    private sealed class StatsBombAwayTeam
    {
        [JsonPropertyName("away_team_name")] public string? AwayTeamName { get; set; }
    }

    private sealed class StatsBombCompetition
    {
        [JsonPropertyName("competition_name")] public string? CompetitionName { get; set; }
    }

    private sealed class StatsBombSeason
    {
        [JsonPropertyName("season_name")] public string? SeasonName { get; set; }
    }
}
