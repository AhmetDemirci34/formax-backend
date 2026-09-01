using Formax.Application.Services.Standings;
using Formax.Domain.Constants;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// MAÇ DETAYI SUNUMU — hangi maçta tablo gösterilir, başlığı ne olur, gösterilmiyorsa
/// kullanıcı ne görür. Karar backend'de tek merkezde verilir.
/// </summary>
public class StandingsPresentationTests
{
    private const int UCL = LockedCompetitions.ChampionsLeague;
    private const int PL  = LockedCompetitions.PremierLeague;

    [Fact]
    public void UlusalLigMaci_NormalTabloGosterir()
    {
        var d = StandingsPresentation.Decide(PL, CompetitionPhase.DomesticLeague, hasLeaguePhaseTable: true);

        Assert.True(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityTable, d.Availability);
        Assert.Equal(StandingsPresentation.DefaultTitle, d.Title);
        Assert.Null(d.Diagnostic);
    }

    [Fact]
    public void ElemeMaci_PuanDurumuGostermez_NotrMetinVerir()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.Qualifying, hasLeaguePhaseTable: true);

        Assert.False(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityNotApplicable, d.Availability);
        Assert.Equal("Bu karşılaşma eleme aşamasındadır. Puan durumu bulunmaz.", d.Notice);
        Assert.Null(d.Title);
    }

    [Fact]
    public void ElemePlayoffMaci_PuanDurumuGostermez()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.QualifyingPlayoff, hasLeaguePhaseTable: true);

        Assert.False(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityNotApplicable, d.Availability);
    }

    [Fact]
    public void LigAsamasiMaci_LigAsamasiTablosunuGosterir()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.LeaguePhase, hasLeaguePhaseTable: true);

        Assert.True(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityLeaguePhase, d.Availability);
        Assert.Equal("Lig Aşaması Puan Durumu", d.Title);
    }

    [Fact]
    public void KnockoutMaci_TabloyuLigAsamasiOlarakEtiketler()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.RoundOf16, hasLeaguePhaseTable: true);

        Assert.True(d.ShowTable);
        // Başlık "Lig Aşaması Puan Durumu" — bu tablo knockout turunun KENDİ tablosu değildir.
        Assert.Equal("Lig Aşaması Puan Durumu", d.Title);
        Assert.Equal(StandingsPresentation.AvailabilityLeaguePhase, d.Availability);
    }

    [Fact]
    public void LigAsamasiBaslamadiysa_SahteTabloUretmez()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.LeaguePhase, hasLeaguePhaseTable: false);

        Assert.False(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityNotAvailable, d.Availability);
        Assert.NotNull(d.Notice);
    }

    [Fact]
    public void UnknownAsama_TabloGostermez_ve_TaniKoduTasir()
    {
        var d = StandingsPresentation.Decide(UCL, CompetitionPhase.Unknown, hasLeaguePhaseTable: true);

        Assert.False(d.ShowTable);
        Assert.Equal(StandingsPresentation.AvailabilityUnresolved, d.Availability);
        Assert.Equal("STANDINGS_PHASE_UNRESOLVED", d.Diagnostic);
        // Kullanıcıya teknik yığın DEĞİL, nötr cümle gösterilir.
        Assert.DoesNotContain("STANDINGS_PHASE_UNRESOLVED", d.Notice);
    }

    [Fact]
    public void SekizUlusalLig_NormalTabloDavranisiniKorur()
    {
        foreach (var leagueId in LockedCompetitions.Domestic)
        {
            var d = StandingsPresentation.Decide(
                leagueId, CompetitionPhase.DomesticLeague, hasLeaguePhaseTable: true);

            Assert.True(d.ShowTable);
            Assert.Equal(StandingsPresentation.AvailabilityTable, d.Availability);
            Assert.Null(d.Diagnostic);
        }
    }
}
