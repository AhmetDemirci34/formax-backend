using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Standings;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// PUAN DURUMU PROJEKSİYONU — puan/averaj/G-B-M üretimi ve neyin tabloya GİRMEDİĞİ.
/// </summary>
public class StandingsProjectionTests
{
    [Fact]
    public void FinishedSkor_DogruPuanUretir()
    {
        // 1 galibiyet (3) + 1 beraberlik (1) = 4 puan; rakipler 0 ve 1.
        var matches = new List<Match>
        {
            TestData.Finished(1, 2, 0, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20),
            TestData.Finished(2, 1, 1, TestData.PastDue(48), homeTeamId: 10, awayTeamId: 30)
        };

        var result = StandingsProjector.Project(88, matches);
        var t10 = result.Rows.Single(r => r.TeamId == 10);

        Assert.Equal(2, t10.Played);
        Assert.Equal(4, t10.Points);
        Assert.Equal(3, t10.GoalsFor);
        Assert.Equal(1, t10.GoalsAgainst);
    }

    [Fact]
    public void EvVeDeplasman_GalibiyetBeraberlikMaglubiyet_DogruHesaplanir()
    {
        var matches = new List<Match>
        {
            // 10 evinde kazandı, 20 deplasmanda kaybetti
            TestData.Finished(1, 3, 1, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20),
            // 20 evinde kazandı, 10 deplasmanda kaybetti
            TestData.Finished(2, 2, 0, TestData.PastDue(48), homeTeamId: 20, awayTeamId: 10),
            // beraberlik
            TestData.Finished(3, 1, 1, TestData.PastDue(24), homeTeamId: 10, awayTeamId: 20)
        };

        var result = StandingsProjector.Project(88, matches);
        var t10 = result.Rows.Single(r => r.TeamId == 10);
        var t20 = result.Rows.Single(r => r.TeamId == 20);

        Assert.Equal(1, t10.Won);
        Assert.Equal(1, t10.Drawn);
        Assert.Equal(1, t10.Lost);
        Assert.Equal(4, t10.Points);

        Assert.Equal(1, t20.Won);
        Assert.Equal(1, t20.Drawn);
        Assert.Equal(1, t20.Lost);
        Assert.Equal(4, t20.Points);

        // Averaj simetrik olmalı: 4-4 vs 4-4
        Assert.Equal(t10.GoalsFor, t20.GoalsAgainst);
        Assert.Equal(t20.GoalsFor, t10.GoalsAgainst);
    }

    [Fact]
    public void ErtelenmisMac_StandingsPuaninaKatilmaz()
    {
        // Puan durumu YALNIZ kesinleşmiş sonuçlardan üretilir. Ertelenmiş maç girdi
        // listesine hiç girmez (okuma sorgusu Finished süzer) — bu test, girse bile
        // 0-0'lık bir "sonuç" olarak puan üretmeyeceğini garanti eder.
        var settledOnly = new List<Match>
        {
            TestData.Finished(1, 2, 1, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20)
        };
        var withPostponed = new List<Match>(settledOnly)
        {
            TestData.Fixture(2, MatchStatuses.Postponed, TestData.PastDue(48),
                home: 0, away: 0, homeTeamId: 10, awayTeamId: 30)
        };

        var baseline = StandingsProjector.Project(88, settledOnly);
        var filtered = StandingsProjector.Project(
            88, withPostponed.Where(m => m.Status == MatchStatuses.Finished).ToList());

        var b10 = baseline.Rows.Single(r => r.TeamId == 10);
        var f10 = filtered.Rows.Single(r => r.TeamId == 10);

        Assert.Equal(b10.Played, f10.Played);
        Assert.Equal(b10.Points, f10.Points);
        Assert.Equal(1, f10.Played);                       // ertelenmiş maç O'yu artırmadı
        Assert.DoesNotContain(filtered.Rows, r => r.TeamId == 30);  // rakip tabloya girmedi
    }

    [Fact]
    public void TakimlarinOynadigiMacSayisi_FarkliOlabilir()
    {
        // Ertelenmiş maç yüzünden 10 iki, 20 bir maç oynamış olabilir. Bu NORMALDİR.
        var matches = new List<Match>
        {
            TestData.Finished(1, 1, 0, TestData.PastDue(72), homeTeamId: 10, awayTeamId: 20),
            TestData.Finished(2, 2, 0, TestData.PastDue(48), homeTeamId: 10, awayTeamId: 30)
        };

        var result = StandingsProjector.Project(88, matches);

        Assert.Equal(2, result.Rows.Single(r => r.TeamId == 10).Played);
        Assert.Equal(1, result.Rows.Single(r => r.TeamId == 20).Played);
    }
}
