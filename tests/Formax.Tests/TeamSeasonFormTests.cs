using System;
using System.Collections.Generic;
using Formax.Application.Services.Matches;
using Formax.Application.Services.Seasons;
using Formax.Application.Services.Standings;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// SEZON FORMU — küçük örneklem ve eksik veri, GENELLEME yapılmasını engellemeli.
/// </summary>
public class TeamSeasonFormTests
{
    private static LeagueSeasonScope Scope() => new(
        LeagueId: 88,
        SeasonYear: 2026,
        StartUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EndUtc: new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Source: "SeasonMetadata");

    private static SeasonDataCompleteness.Result Complete()
        => new(Expected: 10, Included: 10, Missing: 0, IsComplete: true,
               CheckedAtUtc: TestData.Now, MissingMatchIds: Array.Empty<int>(),
               Postponed: 0, Cancelled: 0, Abandoned: 0, StaleResult: 0);

    private static SeasonDataCompleteness.Result Incomplete()
        => new(Expected: 10, Included: 7, Missing: 3, IsComplete: false,
               CheckedAtUtc: TestData.Now, MissingMatchIds: new[] { 1, 2, 3 },
               Postponed: 0, Cancelled: 0, Abandoned: 0, StaleResult: 3);

    [Fact]
    public void KucukOrneklem_Son5Genellemesi_Yapilmaz()
    {
        // 3 maç var: "son 5 maçında" denemez, gerçek kapsam söylenmeli.
        var matches = new List<Match>
        {
            TestData.Finished(1, 2, 0, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20),
            TestData.Finished(2, 1, 0, TestData.PastDue(48), homeTeamId: 10, awayTeamId: 30),
            TestData.Finished(3, 0, 1, TestData.PastDue(24), homeTeamId: 40, awayTeamId: 10)
        };

        var dto = TeamSeasonFormService.Build(
            10, "Takim10", "Eredivisie", Scope(), TestData.Now, matches, Complete());

        Assert.Equal(3, dto.Played);
        Assert.DoesNotContain("son 5", dto.Sentence);
        Assert.Contains("tamamlanan 3 maçta", dto.Sentence);
    }

    [Fact]
    public void YeterliOrneklem_Son5Konusulur()
    {
        var matches = new List<Match>();
        for (var i = 1; i <= 6; i++)
            matches.Add(TestData.Finished(i, 1, 0, TestData.PastDue(24 * i), homeTeamId: 10, awayTeamId: 20 + i));

        var dto = TeamSeasonFormService.Build(
            10, "Takim10", "Eredivisie", Scope(), TestData.Now, matches, Complete());

        Assert.Equal(6, dto.Played);
        Assert.Contains("son 5 maçında", dto.Sentence);
    }

    [Fact]
    public void SezonVerisiEksikse_GenelDegerlendirmeYapilmaz()
    {
        var matches = new List<Match>();
        for (var i = 1; i <= 6; i++)
            matches.Add(TestData.Finished(i, 3, 0, TestData.PastDue(24 * i), homeTeamId: 10, awayTeamId: 20 + i));

        var dto = TeamSeasonFormService.Build(
            10, "Takim10", "Eredivisie", Scope(), TestData.Now, matches, Incomplete());

        Assert.False(dto.IsSeasonDataComplete);
        Assert.Contains("genel form değerlendirmesi yapılmıyor", dto.Sentence);
        Assert.DoesNotContain("son 5 maçında", dto.Sentence);
    }

    [Fact]
    public void EvDeplasman_Ayrimi_DogruHesaplanir()
    {
        var matches = new List<Match>
        {
            TestData.Finished(1, 2, 0, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20), // ev G
            TestData.Finished(2, 1, 0, TestData.PastDue(48), homeTeamId: 30, awayTeamId: 10), // dep M
            TestData.Finished(3, 1, 1, TestData.PastDue(24), homeTeamId: 40, awayTeamId: 10)  // dep B
        };

        var dto = TeamSeasonFormService.Build(
            10, "Takim10", "Eredivisie", Scope(), TestData.Now, matches, Complete());

        Assert.Equal(1, dto.Home.Won);
        Assert.Equal(0, dto.Home.Lost);
        Assert.Equal(1, dto.Away.Lost);
        Assert.Equal(1, dto.Away.Drawn);
        Assert.Equal(1, dto.Won);
        Assert.Equal(1, dto.Drawn);
        Assert.Equal(1, dto.Lost);
    }
}
