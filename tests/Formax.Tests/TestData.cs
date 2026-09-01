using System;
using Formax.Domain.Constants;
using Formax.Domain.Entities;

namespace Formax.Tests;

/// <summary>
/// Kontrollü fikstür üretimi. GERÇEK API ÇAĞRISI YOK — bütün testler bellekte
/// kurulan maçlarla çalışır.
/// </summary>
internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Sonucu beklenecek kadar geçmişte kalmış bir kickoff (210 dk payının ötesinde).</summary>
    public static DateTime PastDue(int hoursAgo = 24) => Now.AddHours(-hoursAgo);

    public static Match Fixture(
        int id,
        string status,
        DateTime? kickoff = null,
        int home = 0,
        int away = 0,
        int leagueId = 88,
        int homeTeamId = 1,
        int awayTeamId = 2)
        => new()
        {
            Id = id,
            Status = status,
            MatchDate = kickoff ?? PastDue(),
            HomeScore = home,
            AwayScore = away,
            LeagueId = leagueId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeTeam = new Team { Id = homeTeamId, Name = $"Takim{homeTeamId}" },
            AwayTeam = new Team { Id = awayTeamId, Name = $"Takim{awayTeamId}" }
        };

    public static Match Finished(int id, int home, int away, DateTime? kickoff = null,
        int leagueId = 88, int homeTeamId = 1, int awayTeamId = 2)
        => Fixture(id, MatchStatuses.Finished, kickoff, home, away, leagueId, homeTeamId, awayTeamId);
}
