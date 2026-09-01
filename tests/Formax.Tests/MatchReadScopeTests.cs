using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// FORM/PUAN KAPSAMI — GERÇEK depo sorgusu (EF InMemory üzerinde) çalıştırılır.
/// Sağlayıcıya HİÇBİR istek gitmez; veri testte kurulur.
///
/// Sözleşme: form penceresine yalnız AYNI LİGİN, BU SEZONUN, TAMAMLANMIŞ maçları girer.
/// </summary>
public class MatchReadScopeTests : IDisposable
{
    private const int League = 88;      // Eredivisie
    private const int CupLeague = 90;   // kupa / hazırlık — kapsam dışı
    private const int Team = 10;

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly FormaxDbContext _db;
    private readonly MatchReadRepository _repo;

    public MatchReadScopeTests()
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"scope-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(options);

        // Kapsam allow-list: yalnız lig 88 ve 90 okunabilir (kupa dahil ki testin
        // eleme sebebinin SEZON/LİG kuralı olduğu belli olsun, allow-list olmasın).
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Coverage:LeagueAllowList:0"] = League.ToString(),
            ["Coverage:LeagueAllowList:1"] = CupLeague.ToString()
        }).Build();

        _repo = new MatchReadRepository(_db, config);
    }

    /// <summary>
    /// Takımlar BİR KEZ eklenir; maçlara yalnız FK verilir. (Navigation nesnesini her
    /// maça ayrı ayrı iliştirmek aynı Id'yi iki kez izletir ve EF hata verir.)
    /// </summary>
    private void Seed(params Match[] matches)
    {
        var teamIds = matches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .ToList();

        foreach (var id in teamIds)
            _db.Teams.Add(new Team { Id = id, Name = $"Takim{id}" });

        foreach (var m in matches)
        {
            m.HomeTeam = null;
            m.AwayTeam = null;
            _db.Matches.Add(m);
        }

        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public void OncekiSezonMaclari_FormHesabinaKatilmaz()
    {
        Seed(
            TestData.Finished(1, 3, 0, new DateTime(2026, 5, 10, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 20),          // ÖNCEKİ sezon
            TestData.Finished(2, 1, 0, new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30));         // bu sezon

        var result = _repo.GetSeasonLeagueMatchesForTeam(Team, League, SeasonStart, Now);

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void FriendlyVeKupaMaclari_FormHesabinaKatilmaz()
    {
        Seed(
            TestData.Finished(1, 4, 0, new DateTime(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc),
                leagueId: CupLeague, homeTeamId: Team, awayTeamId: 20),       // kupa/hazırlık
            TestData.Finished(2, 1, 1, new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30));         // lig

        var result = _repo.GetSeasonLeagueMatchesForTeam(Team, League, SeasonStart, Now);

        Assert.Single(result);
        Assert.Equal(League, result[0].LeagueId);
    }

    [Fact]
    public void YalnizFinishedMaclar_FormaKatilir()
    {
        Seed(
            TestData.Finished(1, 2, 0, new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 20),
            TestData.Fixture(2, MatchStatuses.NotStarted, new DateTime(2026, 8, 18, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30),
            TestData.Fixture(3, MatchStatuses.Postponed, new DateTime(2026, 8, 22, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 40),
            TestData.Fixture(4, MatchStatuses.Live, new DateTime(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 50));

        var result = _repo.GetSeasonLeagueMatchesForTeam(Team, League, SeasonStart, Now);

        Assert.Single(result);
        Assert.Equal(MatchStatuses.Finished, result[0].Status);
    }

    [Fact]
    public void ErtelenmisMac_StandingsGirdisineAlinmaz()
    {
        Seed(
            TestData.Finished(1, 2, 1, new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 20),
            TestData.Fixture(2, MatchStatuses.Postponed, new DateTime(2026, 8, 22, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30));

        var settled = _repo.GetSettledLeagueMatchesInSeason(League, SeasonStart, Now.AddYears(1));

        Assert.Single(settled);
        Assert.DoesNotContain(settled, m => m.Status == MatchStatuses.Postponed);
    }

    [Fact]
    public void TamlikSorgusu_DurumSuzmez_ErtelenmisiDeGetirir()
    {
        // Tamlık hesabı beklenen ile kesinleşeni KARŞILAŞTIRDIĞI için ertelenmişi de
        // görmelidir; elemeyi SeasonDataCompleteness yapar, sorgu değil.
        Seed(
            TestData.Finished(1, 2, 1, new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 20),
            TestData.Fixture(2, MatchStatuses.Postponed, new DateTime(2026, 8, 22, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30));

        var fixtures = _repo.GetSeasonLeagueFixturesBefore(League, SeasonStart, Now.AddYears(1), Now);

        Assert.Equal(2, fixtures.Count);
        Assert.Contains(fixtures, m => m.Status == MatchStatuses.Postponed);
    }

    [Fact]
    public void GelecekVeCanliMaclar_FormaKatilmaz()
    {
        var kickoff = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        Seed(
            TestData.Finished(1, 2, 0, new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 20),
            TestData.Fixture(2, MatchStatuses.Live, new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 30),
            TestData.Finished(3, 1, 0, new DateTime(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc),
                leagueId: League, homeTeamId: Team, awayTeamId: 40));   // hedef maçtan SONRA

        var result = _repo.GetSeasonLeagueMatchesForTeam(Team, League, SeasonStart, kickoff);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void UefaElemeMaci_UefaSezonFormunaGirer()
    {
        // UEFA'da mevcut sezon formu turnuvanın BÜTÜN aşamalarını kapsar: eleme de dahildir.
        const int Ucl = Formax.Domain.Constants.LockedCompetitions.ChampionsLeague;
        var uclSeasonStart = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);
        var kickoff = new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc);

        var qualifying = TestData.Finished(1, 2, 1, new DateTime(2026, 7, 29, 18, 0, 0, DateTimeKind.Utc),
            leagueId: Ucl, homeTeamId: Team, awayTeamId: 20);
        qualifying.Round = "2nd Qualifying Round";
        var playoff = TestData.Finished(2, 1, 0, new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
            leagueId: Ucl, homeTeamId: Team, awayTeamId: 30);
        playoff.Round = "Play-offs";

        // Kapsam allow-list'ine UCL'yi de alan ayrı bir depo örneği gerekir.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Coverage:LeagueAllowList:0"] = Ucl.ToString()
        }).Build();
        var repo = new MatchReadRepository(_db, config);

        Seed(qualifying, playoff);

        var form = repo.GetSeasonLeagueMatchesForTeam(Team, Ucl, uclSeasonStart, kickoff);

        Assert.Equal(2, form.Count);   // eleme + eleme play-off FORMA GİRER
    }

    public void Dispose() => _db.Dispose();
}
