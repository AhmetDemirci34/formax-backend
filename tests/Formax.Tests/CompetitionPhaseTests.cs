using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Standings;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// UEFA AŞAMA SINIFLANDIRMASI — puan tablosuna YALNIZ lig aşaması girer.
///
/// Tur adları depodaki GERÇEK sağlayıcı değerlerinden alınmıştır
/// ("2nd Qualifying Round", "Play-offs", "Playoff round").
/// </summary>
public class CompetitionPhaseTests
{
    private const int UCL = LockedCompetitions.ChampionsLeague;
    private const int UEL = LockedCompetitions.EuropaLeague;
    private const int UECL = LockedCompetitions.ConferenceLeague;

    // ── Sınıflandırma ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1st Qualifying Round", CompetitionPhase.Qualifying)]
    [InlineData("2nd Qualifying Round", CompetitionPhase.Qualifying)]
    [InlineData("3rd Qualifying Round", CompetitionPhase.Qualifying)]
    [InlineData("Preliminary Round", CompetitionPhase.Qualifying)]
    [InlineData("Play-offs", CompetitionPhase.QualifyingPlayoff)]
    [InlineData("Playoff round", CompetitionPhase.QualifyingPlayoff)]
    [InlineData("Qualifying Play-offs", CompetitionPhase.QualifyingPlayoff)]
    [InlineData("League Phase - 1", CompetitionPhase.LeaguePhase)]
    [InlineData("League Stage - 8", CompetitionPhase.LeaguePhase)]
    [InlineData("Knockout Round Play-offs", CompetitionPhase.KnockoutPlayoff)]
    [InlineData("Round of 16", CompetitionPhase.RoundOf16)]
    [InlineData("Quarter-finals", CompetitionPhase.QuarterFinal)]
    [InlineData("Semi-finals", CompetitionPhase.SemiFinal)]
    [InlineData("Final", CompetitionPhase.Final)]
    public void UefaTurAdlari_DogruAsamayaEslenir(string round, CompetitionPhase expected)
        => Assert.Equal(expected, CompetitionPhaseResolver.Resolve(UCL, round));

    [Fact]
    public void ElemePlayoff_ile_KnockoutPlayoff_AyriAsamalardir()
    {
        var qualifying = CompetitionPhaseResolver.Resolve(UEL, "Play-offs");
        var knockout   = CompetitionPhaseResolver.Resolve(UEL, "Knockout Round Play-offs");

        Assert.NotEqual(qualifying, knockout);
        Assert.Equal(CompetitionPhase.QualifyingPlayoff, qualifying);
        Assert.Equal(CompetitionPhase.KnockoutPlayoff, knockout);
        // İKİSİ DE tablo dışıdır.
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(qualifying));
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(knockout));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Group Stage")]      // eski format; lig aşaması DEĞİL
    [InlineData("Bilinmeyen Tur")]
    public void CozulemeyenTur_Unknown_Olur(string? round)
        => Assert.Equal(CompetitionPhase.Unknown, CompetitionPhaseResolver.Resolve(UCL, round));

    [Fact]
    public void UlusalLig_DomesticLeague_Olur()
        => Assert.Equal(CompetitionPhase.DomesticLeague,
            CompetitionPhaseResolver.Resolve(LockedCompetitions.PremierLeague, "Regular Season - 3"));

    // ── Tabloya giriş kuralı ──────────────────────────────────────────────────

    [Fact]
    public void UclEleme_StandingsDisinda()
    {
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UCL, "2nd Qualifying Round")));
    }

    [Fact]
    public void UelElemePlayoff_StandingsDisinda()
    {
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UEL, "Play-offs")));
    }

    [Fact]
    public void UeclEleme_StandingsDisinda()
    {
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UECL, "Playoff round")));
        Assert.False(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UECL, "2nd Qualifying Round")));
    }

    [Fact]
    public void LigAsamasi_StandingsIcinde()
    {
        Assert.True(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UCL, "League Phase - 3")));
    }

    [Theory]
    [InlineData("Knockout Round Play-offs")]
    [InlineData("Round of 16")]
    [InlineData("Quarter-finals")]
    [InlineData("Semi-finals")]
    [InlineData("Final")]
    public void KnockoutAsamalari_StandingsDisinda(string round)
        => Assert.False(CompetitionPhaseResolver.CountsTowardStandings(
            CompetitionPhaseResolver.Resolve(UCL, round)));

    [Fact]
    public void UnknownAsama_StandingsDisinda()
        => Assert.False(CompetitionPhaseResolver.CountsTowardStandings(CompetitionPhase.Unknown));

    // ── Tamlık: yalnız lig aşaması ────────────────────────────────────────────

    private static Match Ufx(int id, string status, string? round, DateTime kickoff, int leagueId = UCL)
        => new()
        {
            Id = id, Status = status, Round = round, MatchDate = kickoff,
            LeagueId = leagueId, HomeTeamId = 1, AwayTeamId = 2
        };

    [Fact]
    public void UefaTamlik_YalnizLigAsamasiniSayar()
    {
        var past = TestData.PastDue();
        var fixtures = new List<Match>
        {
            // Eleme: sonucu gelmemiş olsa bile tamlığı BOZMAZ
            Ufx(1, MatchStatuses.NotStarted, "2nd Qualifying Round", past),
            Ufx(2, MatchStatuses.NotStarted, "Play-offs", past),
            // Knockout: yine tamlık dışı
            Ufx(3, MatchStatuses.NotStarted, "Round of 16", past),
            // Lig aşaması: biri tamam, biri eksik
            Ufx(4, MatchStatuses.Finished, "League Phase - 1", past),
            Ufx(5, MatchStatuses.NotStarted, "League Phase - 2", past)
        };

        var r = SeasonDataCompleteness.EvaluateForStandings(UCL, fixtures, TestData.Now);

        Assert.Equal(2, r.Expected);      // yalnız iki lig aşaması maçı
        Assert.Equal(1, r.Included);
        Assert.Equal(1, r.Missing);
        Assert.False(r.IsComplete);
    }

    [Fact]
    public void UefaTamlik_LigAsamasiYokken_EksikUretmez()
    {
        // 01.09.2026 gerçek durumu: depoda yalnız eleme maçları var, lig aşaması başlamadı.
        var past = TestData.PastDue();
        var fixtures = new List<Match>
        {
            Ufx(1, MatchStatuses.NotStarted, "2nd Qualifying Round", past),
            Ufx(2, MatchStatuses.NotStarted, "Playoff round", past, UECL),
            Ufx(3, MatchStatuses.NotStarted, null, past)      // aşaması çözülemeyen eski kayıt
        };

        var r = SeasonDataCompleteness.EvaluateForStandings(UCL, fixtures, TestData.Now);

        Assert.Equal(0, r.Expected);
        Assert.Equal(0, r.Missing);
        Assert.True(r.IsComplete);        // eleme eksikleri tabloyu EKSİK yapmaz
    }

    [Fact]
    public void UlusalLikTamlik_AsamaSuzgecindenEtkilenmez()
    {
        var past = TestData.PastDue();
        var fixtures = new List<Match>
        {
            Ufx(1, MatchStatuses.Finished, "Regular Season - 1", past, LockedCompetitions.PremierLeague),
            Ufx(2, MatchStatuses.NotStarted, "Regular Season - 2", past, LockedCompetitions.PremierLeague)
        };

        var r = SeasonDataCompleteness.EvaluateForStandings(
            LockedCompetitions.PremierLeague, fixtures, TestData.Now);

        Assert.Equal(2, r.Expected);
        Assert.Equal(1, r.Missing);
    }

    // ── Projeksiyon: eleme maçları tabloya giremez ────────────────────────────

    [Fact]
    public void ElemeMaclari_PuanTablosuUretmez()
    {
        var settled = new List<Match>
        {
            TestData.Finished(1, 3, 0, TestData.PastDue(72), leagueId: UCL, homeTeamId: 10, awayTeamId: 20),
            TestData.Finished(2, 1, 0, TestData.PastDue(48), leagueId: UCL, homeTeamId: 10, awayTeamId: 30)
        };
        settled[0].Round = "2nd Qualifying Round";
        settled[1].Round = "Play-offs";

        var leaguePhaseOnly = settled
            .Where(m => CompetitionPhaseResolver.Resolve(UCL, m.Round) == CompetitionPhase.LeaguePhase)
            .ToList();

        var projection = StandingsProjector.Project(UCL, leaguePhaseOnly);

        Assert.Empty(leaguePhaseOnly);
        Assert.Empty(projection.Rows);     // sahte tablo YOK
    }
}
