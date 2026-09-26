using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Xunit;
using Pred = Formax.Application.Services.Outcomes.OutcomeAccuracyLab.Pred;

namespace Formax.Tests;

/// <summary>
/// TAHMİN DOĞRULUĞU KAPANIŞI (26.09.2026) — sızıntı düzeltmesi (eşzamanlı maçlar), zamansal bölmeler, türetme ve ölçüm
/// araçlarının sözleşmeleri. Üretim modeli 4.0 DEĞİŞMEDİ (aday final testte kabul kuralını geçemedi).
/// </summary>
public class OutcomeAccuracyTests
{
    private static readonly DateTime Day0 = new(2024, 1, 6, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Deterministik lig takvimi: her tur aynı başlama saatinde birden çok maç (gerçek hafta sonu düzeni).</summary>
    private static List<HistoricalMatch> League(int leagueId, int teams, int rounds, int seed)
    {
        var rng = new Random(seed);
        var list = new List<HistoricalMatch>();
        var id = leagueId * 100000;
        var r = 0;
        for (var round = 0; round < rounds; round++)
            for (var h = 0; h < teams; h++)
                for (var a = 0; a < teams; a++)
                {
                    if (h == a || (h + a + round) % teams != 0) continue;
                    var kick = Day0.AddDays(7 * (r++ / Math.Max(1, teams / 2))); // her 5 maç aynı başlama saatinde
                    list.Add(new HistoricalMatch(++id, kick, leagueId, leagueId * 1000 + h, leagueId * 1000 + a, rng.Next(0, 4), rng.Next(0, 3)));
                }
        return list.OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
    }

    private static (OutcomeBacktestReport Report, OutcomeBacktest.BacktestArtifacts Art) Run(List<HistoricalMatch> history)
    {
        var catalog = CompetitionCatalog.Build(history);
        var end = history[^1].KickoffUtc.AddDays(1);
        var testStart = history[(int)(history.Count * 0.7)].KickoffUtc;
        var calStart = history[(int)(history.Count * 0.45)].KickoffUtc;
        var r = OutcomeBacktest.Run(history, catalog, new HashSet<int> { 39 }, history[0].KickoffUtc, calStart, testStart, end, end,
            compareLegacy: false, candidate: true);
        return (r, OutcomeBacktest.LastArtifacts!);
    }

    [Fact]
    public void GelecekVeri_Sizmaz_AyniSaattekiMacinSonucu_TahmineGirmez()
    {
        var history = League(39, 10, 30, 7);
        var (_, art) = Run(history);
        var target = art.LockedTestSamples.First(s => history.Count(m => m.KickoffUtc == s.KickoffUtc) > 1);
        // Aynı saatte başlayan BAŞKA bir maçın skorunu değiştir: hedef maçın tahmini birebir aynı kalmalı.
        var sibling = history.First(m => m.KickoffUtc == target.KickoffUtc && m.MatchId != target.MatchId);
        var altered = history.Select(m => m.MatchId == sibling.MatchId ? m with { HomeGoals = 9, AwayGoals = 0 } : m).ToList();
        var (_, art2) = Run(altered);
        var t2 = art2.LockedTestSamples.First(s => s.MatchId == target.MatchId);
        Assert.Equal(target.E.LambdaHome, t2.E.LambdaHome, 12);
        Assert.Equal(target.E.LambdaAway, t2.E.LambdaAway, 12);
        Assert.Equal(target.BaseHome, t2.BaseHome, 12);
        Assert.Equal(target.BaseOver25, t2.BaseOver25, 12);
    }

    [Fact]
    public void AyniMac_KendiTahminini_Etkilemez()
    {
        var history = League(39, 10, 30, 11);
        var (_, art) = Run(history);
        var target = art.LockedTestSamples[art.LockedTestSamples.Count / 2];
        var altered = history.Select(m => m.MatchId == target.MatchId ? m with { HomeGoals = 0, AwayGoals = 7 } : m).ToList();
        var (_, art2) = Run(altered);
        var t2 = art2.LockedTestSamples.First(s => s.MatchId == target.MatchId);
        Assert.Equal(target.E.LambdaHome, t2.E.LambdaHome, 12);
        Assert.Equal(target.E.EloHomeExpectation, t2.E.EloHomeExpectation, 12);
    }

    [Fact]
    public void Bolmeler_TarihVeMatchId_Cakismaz()
    {
        var history = League(39, 10, 30, 3);
        var (r, art) = Run(history);
        var val = art.CalibrationSamples.Select(s => s.MatchId).ToHashSet();
        var test = art.LockedTestSamples.Select(s => s.MatchId).ToHashSet();
        Assert.False(val.Overlaps(test));
        Assert.True(art.CalibrationSamples.Max(s => s.KickoffUtc) < art.LockedTestSamples.Min(s => s.KickoffUtc));
        Assert.True(art.LockedTestSamples.All(s => s.KickoffUtc >= r.TestStartUtc && s.KickoffUtc < r.TestEndUtc));
    }

    [Fact]
    public void EvDeplasmanYonu_Korunur_VeBootstrapDeterministik()
    {
        var history = League(39, 10, 30, 5);
        var (_, art) = Run(history);
        var byId = history.ToDictionary(m => m.MatchId);
        Assert.All(art.LockedTestSamples, s => { Assert.Equal(byId[s.MatchId].HomeGoals, s.HomeGoals); Assert.Equal(byId[s.MatchId].AwayGoals, s.AwayGoals); });
        var diffs = art.LockedTestSamples.Select((s, i) => (s.KickoffUtc, (i % 3 - 1) * 0.01)).ToList();
        Assert.Equal(OutcomeAccuracyLab.PairedBlockBootstrap(diffs), OutcomeAccuracyLab.PairedBlockBootstrap(diffs));
    }

    [Fact]
    public void Olasiliklar_ToplamBir_IkiliMarketler_SifirBirArasi_CifteSansDogruTuretilir()
    {
        var d = ScoreDistribution.Poisson(1.7, 0.9);
        var p = Pred.From(d);
        Assert.Equal(1.0, p.H + p.D + p.A, 9);
        foreach (var q in new[] { p.O15, p.O25, p.O35, p.Btts }) Assert.InRange(q, 0, 1);
        var (x1, x2, h12) = p.DoubleChance;
        Assert.Equal(p.H + p.D, x1, 12); Assert.Equal(p.D + p.A, x2, 12); Assert.Equal(p.H + p.A, h12, 12);

        // Sonuç sınıfı yeniden ağırlıklandırma: hedef marjinaller tutar, toplam 1, gol marketleri 0–1.
        var r = d.ReweightResult(0.40, 0.30, 0.30);
        Assert.Equal(0.40, r.HomeWin, 9); Assert.Equal(0.30, r.Draw, 9); Assert.Equal(0.30, r.AwayWin, 9);
        Assert.Equal(1.0, r.HomeWin + r.Draw + r.AwayWin, 9);
        Assert.InRange(r.Over(2.5), 0, 1); Assert.InRange(r.BttsYes, 0, 1);
        Assert.Same(d, d.ReweightResult(0, 0.5, 0.5)); // geçersiz hedef → değişmez
    }

    [Fact]
    public void Elo_AyniSaattekiMaclariBirbirindenBagimsiz_VeTahminGuncellemedenOnce()
    {
        var history = League(39, 10, 12, 9);
        var x = OutcomeAccuracyLab.EloLogits(history, DateTime.MaxValue);
        var target = history.Skip(history.Count / 2).First(m => history.Count(o => o.KickoffUtc == m.KickoffUtc) > 1);
        var sibling = history.First(m => m.KickoffUtc == target.KickoffUtc && m.MatchId != target.MatchId);
        var altered = history.Select(m => m.MatchId == sibling.MatchId || m.MatchId == target.MatchId ? m with { HomeGoals = 8, AwayGoals = 0 } : m).ToList();
        var y = OutcomeAccuracyLab.EloLogits(altered, DateTime.MaxValue);
        Assert.Equal(x[target.MatchId], y[target.MatchId], 12);
        var p = OutcomeAccuracyLab.EloPred(x[target.MatchId], 0.5);
        Assert.Equal(1.0, p.H + p.D + p.A, 9);
    }

    [Fact]
    public void Ablasyon_YalnizHedefBileseni_Notrlestirir()
    {
        var p = new OutcomeModelParameters { BaselineMix = 0.12, UncertaintyMix = 0.4, DrawInflation = 1.08, GoalScale = 0.96, TotalGoalShrink = 0.5 };
        p.LeagueGoalScale[39] = 1.02;
        var a = OutcomeAccuracyLab.Ablate(p, "TotalGoalShrink");
        Assert.Equal(1.0, a.TotalGoalShrink); Assert.Equal(0.12, a.BaselineMix); Assert.Equal(1.02, a.LeagueGoalScale[39]);
        Assert.Equal(0.5, p.TotalGoalShrink); // özgün parametre değişmez
    }

    [Fact]
    public void UretimModeli_Degismedi_ConfigParmakIzi_YontemSurumunuTasir()
    {
        Assert.Equal("formax-outcome-4.0", OutcomeModelVersion.Current);
        Assert.Equal("kickoff-batch-1", OutcomeBacktest.MethodVersion);
        var a = EligibilityPublicationPolicy.ConfigHash(OutcomeModelVersion.Current, TimeSpan.FromDays(600), TimeSpan.FromDays(150), TimeSpan.FromDays(150), LockedCompetitions.All);
        Assert.Equal(a, EligibilityPublicationPolicy.ConfigHash(OutcomeModelVersion.Current, TimeSpan.FromDays(600), TimeSpan.FromDays(150), TimeSpan.FromDays(150), LockedCompetitions.All));
        // Üretim tahmincisi laboratuvar adaylarını KULLANMAZ (Elo karışımı / dinlenme düzeltmesi yok).
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        foreach (var f in new[] { Path.Combine(dir!, "Formax.Application", "Services", "Outcomes", "OutcomeModel.cs"), Path.Combine(dir!, "Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs") })
        {
            var src = File.ReadAllText(f);
            Assert.DoesNotContain("OutcomeAccuracyLab", src);
            Assert.DoesNotContain("ReweightResult(", src.Replace("public ScoreDistribution ReweightResult(", ""));
        }
    }
}
