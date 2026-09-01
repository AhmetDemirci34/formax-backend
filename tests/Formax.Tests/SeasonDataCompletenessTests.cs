using System;
using System.Collections.Generic;
using Formax.Application.Services.Standings;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// PUAN DURUMU TAMLIK KURALI.
///
/// Sözleşme: oynanmamış maç (ertelendi/iptal/yarıda kaldı) tabloyu EKSİK YAPMAZ.
/// Sonucu beklenen ama gelmeyen maç ise tabloyu eksik YAPAR.
/// </summary>
public class SeasonDataCompletenessTests
{
    [Fact]
    public void NormalGecmisMac_SonucuYoksa_MissingSayilir()
    {
        var fixtures = new List<Match>
        {
            TestData.Finished(1, 2, 1),
            TestData.Fixture(2, MatchStatuses.NotStarted)   // kickoff geçti, sonuç yok
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(2, r.Expected);
        Assert.Equal(1, r.Included);
        Assert.Equal(1, r.Missing);
        Assert.False(r.IsComplete);
        Assert.Contains(2, r.MissingMatchIds);
    }

    [Fact]
    public void ErtelenmisMac_MissingSayilmaz()
    {
        var fixtures = new List<Match>
        {
            TestData.Finished(1, 1, 0),
            TestData.Fixture(2, MatchStatuses.Postponed)
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(0, r.Missing);
        Assert.Equal(1, r.Postponed);
        Assert.Equal(1, r.Expected);          // ertelenmiş maç BEKLENEN değildir
        Assert.DoesNotContain(2, r.MissingMatchIds);
    }

    [Fact]
    public void IptalEdilmisMac_MissingSayilmaz()
    {
        var fixtures = new List<Match>
        {
            TestData.Finished(1, 0, 0),
            TestData.Fixture(2, MatchStatuses.Cancelled)
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(0, r.Missing);
        Assert.Equal(1, r.Cancelled);
        Assert.True(r.IsComplete);
    }

    [Fact]
    public void YaridaKalmisMac_MissingSayilmaz()
    {
        var fixtures = new List<Match>
        {
            TestData.Finished(1, 3, 2),
            TestData.Fixture(2, MatchStatuses.Abandoned)
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(0, r.Missing);
        Assert.Equal(1, r.Abandoned);
        Assert.True(r.IsComplete);
    }

    [Fact]
    public void ErtelenmisMacVarken_IsComplete_TrueOlabilir()
    {
        // EREDIVISIE SENARYOSU: 33 maç oynandı ve sonuçlandı, 1 maç (NEC–Excelsior) ertelendi.
        var fixtures = new List<Match>();
        for (var i = 1; i <= 33; i++) fixtures.Add(TestData.Finished(i, 1, 0));
        fixtures.Add(TestData.Fixture(999, MatchStatuses.Postponed));

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(1, r.Postponed);
        Assert.Equal(0, r.Missing);
        Assert.True(r.IsComplete);            // tablo GÜNCEL gösterilebilir
        Assert.Equal(33, r.Expected);
        Assert.Equal(33, r.Included);
    }

    [Fact]
    public void HenuzOynanmakta_OlanMac_EksiklikSayilmaz()
    {
        // Kickoff 30 dk önce: 210 dk payı dolmadı, bu bir eksiklik DEĞİLDİR.
        var fixtures = new List<Match>
        {
            TestData.Fixture(1, MatchStatuses.Live, TestData.Now.AddMinutes(-30))
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(0, r.Expected);
        Assert.Equal(0, r.Missing);
        Assert.True(r.IsComplete);
    }

    [Fact]
    public void StaleResult_SonucBeklenenAmaGelmeyenleriSayar()
    {
        var fixtures = new List<Match>
        {
            TestData.Finished(1, 1, 1),
            TestData.Fixture(2, MatchStatuses.NotStarted),
            TestData.Fixture(3, MatchStatuses.Live),
            TestData.Fixture(4, MatchStatuses.Postponed)   // sayılmamalı
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(2, r.StaleResult);
        Assert.Equal(2, r.Missing);
        Assert.Equal(1, r.Postponed);
    }

    [Fact]
    public void OynanmamisMaclarSayilir_AmaBekleneninDisindaTutulur()
    {
        var fixtures = new List<Match>
        {
            TestData.Fixture(1, MatchStatuses.Postponed),
            TestData.Fixture(2, MatchStatuses.Cancelled),
            TestData.Fixture(3, MatchStatuses.Abandoned)
        };

        var r = SeasonDataCompleteness.Evaluate(fixtures, TestData.Now);

        Assert.Equal(3, r.NotPlayed);
        Assert.Equal(0, r.Expected);
        Assert.Equal(0, r.Missing);
        Assert.True(r.IsComplete);
    }
}
