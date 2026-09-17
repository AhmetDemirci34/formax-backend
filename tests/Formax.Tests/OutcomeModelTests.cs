using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Services.Odds;
using Formax.Application.Services.Outcomes;
using Formax.Application.Services.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Tests;

/// <summary>OLASI SONUÇ MOTORU (15.09.2026) — tek skor dağılımı, ana kart kuralı, kalibrasyon ve snapshot sözleşmesi.</summary>
public class OutcomeModelTests
{
    private static OutcomeExpectation E(double lh, double la, double coverage = 1, int n = 20)
        => new(lh, la, 1.45, 1.15, n, n, coverage, n >= 4, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);

    private static OutcomeSnapshotDto Snap(double lh, double la, double coverage = 1, OutcomeModelParameters? p = null)
        => OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(lh, la, coverage), 140, p ?? new OutcomeModelParameters()), "Ev", "Dep");

    private static int P(OutcomeSnapshotDto s, string market) => s.Families.SelectMany(f => f.Items).Single(i => i.Market == market).Probability;
    private static double C(OutcomeSnapshotDto s, string market) => s.Families.SelectMany(f => f.Items).Single(i => i.Market == market).CalibratedProbability;

    [Theory]
    [InlineData(2.4, 0.6)]   // güçlü ev favorisi
    [InlineData(1.2, 1.2)]   // dengeli
    [InlineData(0.7, 0.6)]   // düşük gol
    [InlineData(2.3, 2.1)]   // yüksek gol
    [InlineData(0.5, 2.2)]   // güçlü deplasman favorisi
    public void TekDagilim_ToplamlarYuzde100_CifteSansFormulleri_Tutarli(double lh, double la)
    {
        var s = Snap(lh, la);
        Assert.True(s.Checks!.Consistent);
        Assert.Equal(1.0, C(s, "Ev Sahibi Kazanır") + C(s, "Beraberlik") + C(s, "Deplasman Kazanır"), 3);
        Assert.Equal(1.0, C(s, "Karşılıklı Gol Var") + C(s, "Karşılıklı Gol Yok"), 3);
        foreach (var l in new[] { "1.5", "2.5", "3.5" }) Assert.Equal(1.0, C(s, l + " Üst") + C(s, l + " Alt"), 3);
        Assert.Equal(C(s, "Ev Sahibi Kazanır") + C(s, "Beraberlik"), C(s, "Çifte Şans (1X)"), 3);
        Assert.Equal(C(s, "Beraberlik") + C(s, "Deplasman Kazanır"), C(s, "Çifte Şans (X2)"), 3);
        Assert.Equal(C(s, "Ev Sahibi Kazanır") + C(s, "Deplasman Kazanır"), C(s, "Çifte Şans (1-2)"), 3);
        // Gösterilen tamsayılar da tutarlı.
        Assert.Equal(100, P(s, "Ev Sahibi Kazanır") + P(s, "Beraberlik") + P(s, "Deplasman Kazanır"));
        Assert.Equal(100, P(s, "Karşılıklı Gol Var") + P(s, "Karşılıklı Gol Yok"));
        Assert.Equal(P(s, "Ev Sahibi Kazanır") + P(s, "Beraberlik"), P(s, "Çifte Şans (1X)"));
    }

    [Theory]
    [InlineData(2.4, 0.6)]
    [InlineData(1.2, 1.2)]
    [InlineData(0.7, 0.6)]
    [InlineData(2.3, 2.1)]
    [InlineData(0.5, 2.2)]
    public void AnaKartlar_UcFarkliAile_CifteSansAsla_YokOranGirdisiYok(double lh, double la)
    {
        var s = Snap(lh, la);
        Assert.Equal(3, s.MainCards.Count);
        Assert.Equal(new[] { OutcomeFamilies.Result, OutcomeFamilies.Goals, OutcomeFamilies.Btts }, s.MainCards.Select(c => c.Family).ToArray());
        Assert.DoesNotContain(s.MainCards, c => c.MarketKey != null && OutcomeFamilies.IsCompound(c.MarketKey));
        Assert.All(s.MainCards, c => Assert.False(string.IsNullOrWhiteSpace(c.Reason)));
        Assert.All(s.MainCards, c => Assert.NotEmpty(c.ReasonCodes));
        // Oran hiçbir girdide yok: kurucu imzası yalnız dağılım + takım adı alır.
        Assert.DoesNotContain(typeof(OutcomeSnapshotBuilder).GetMethod("Build")!.GetParameters(), p => p.Name!.Contains("odd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnaKart_HamYuzdeyeGoreSecilmez_BilgiFarkiSecer()
    {
        // Ev favorisinde 1.5 Üst ham yüzdesi en yüksek olsa da ana gol kartı tabanına göre bilgi farkıyla seçilir.
        var s = Snap(2.6, 1.6);
        var goals = s.Families.Single(f => f.Family == OutcomeFamilies.Goals).Items;
        var highestRaw = goals.OrderByDescending(g => g.CalibratedProbability).First();
        var main = s.MainCards.Single(c => c.Family == OutcomeFamilies.Goals);
        Assert.Equal(goals.Where(g => g.CalibratedProbability >= 0.5).Max(g => g.SelectionScore), main.SelectionScore);
        Assert.True(main.SelectionScore >= highestRaw.SelectionScore);
        Assert.True(main.InformationLift > 0);
    }

    [Fact]
    public void EksikVeri_BelirsizlikArtar_YuzdelerTabanaYaklasir_YetersizVerideTahminYok()
    {
        var p = new OutcomeModelParameters { UncertaintyMix = 0.5 };
        var rich = Snap(2.4, 0.6, 1.0, p);
        var thin = Snap(2.4, 0.6, 0.3, p);
        var baseline = C(rich, "Ev Sahibi Kazanır") - rich.Families.SelectMany(f => f.Items).Single(i => i.Market == "Ev Sahibi Kazanır").InformationLift;
        Assert.True(Math.Abs(C(thin, "Ev Sahibi Kazanır") - baseline) < Math.Abs(C(rich, "Ev Sahibi Kazanır") - baseline));
        Assert.NotNull(thin.Limitation);
        Assert.True(thin.MainCards[0].Uncertainty > rich.MainCards[0].Uncertainty);

        var insufficient = OutcomeSnapshotBuilder.Insufficient(1, E(1, 1, 0.1, n: 2), "Ev", "Dep");
        Assert.Equal("InsufficientData", insufficient.Status);
        Assert.Empty(insufficient.MainCards);
        Assert.Equal(OutcomeSnapshotBuilder.NotEligibleNotice, insufficient.Notice);
        Assert.Equal(PredictionEligibilities.Disabled, insufficient.PredictionEligibility);
    }

    [Fact]
    public void ZamansalSizintiYok_TahminYalnizOncekiMaclardanKurulur()
    {
        var p = new OutcomeModelParameters();
        var history = new List<HistoricalMatch>();
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rnd = new Random(7);
        for (var i = 0; i < 400; i++)
            history.Add(new HistoricalMatch(i, t.AddDays(i), 140, rnd.Next(1, 11), rnd.Next(11, 21), rnd.Next(0, 4), rnd.Next(0, 3)));
        var cut = t.AddDays(200);
        var a = new OutcomeRatingModel(p);
        foreach (var m in history.Where(m => m.KickoffUtc < cut)) a.Update(m);
        var before = a.Expect(140, 3, 13, cut);

        // Aynı tahmin, geleceğin maçları "bilinse" de değişmemeli: model yalnız sıralı işlenir.
        var b = new OutcomeRatingModel(p);
        OutcomeExpectation? atCut = null;
        foreach (var m in history)
        {
            if (atCut == null && m.KickoffUtc >= cut) atCut = b.Expect(140, 3, 13, cut);
            b.Update(m);
        }
        Assert.Equal(before.LambdaHome, atCut!.LambdaHome, 10);
        Assert.Equal(before.LambdaAway, atCut.LambdaAway, 10);
        Assert.NotEqual(before.LambdaHome, b.Expect(140, 3, 13, cut).LambdaHome);
    }

    private static List<HistoricalMatch> Synthetic(int count, int seed, Func<int, (int H, int A)>? outcome = null)
    {
        var rnd = new Random(seed);
        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var strength = Enumerable.Range(0, 21).Select(_ => 0.6 + rnd.NextDouble() * 1.0).ToArray();
        var list = new List<HistoricalMatch>();
        for (var i = 0; i < count; i++)
        {
            var h = rnd.Next(1, 21); var a = rnd.Next(1, 21); if (a == h) a = h % 20 + 1;
            (int H, int A) g = outcome?.Invoke(i) ?? (Poisson(rnd, 1.4 * strength[h] / strength[a]), Poisson(rnd, 1.1 * strength[a] / strength[h]));
            list.Add(new HistoricalMatch(i + 1, start.AddHours(i * 9), 140, h, a, g.H, g.A));
        }
        return list;
    }

    private static int Poisson(Random r, double l) { var L = Math.Exp(-l); var k = 0; var p = 1.0; do { k++; p *= r.NextDouble(); } while (p > L); return k - 1; }

    [Fact]
    public void Backtest_Metrikler_KalibrasyonTestPenceresineBakmadanSecilir()
    {
        var data = Synthetic(2600, 11);
        var start = data[0].KickoffUtc;
        DateTime At(int i) => data[i].KickoffUtc;
        var report = OutcomeBacktest.Run(data, new HashSet<int> { 140 }, At(300), At(1200), At(2000), At(2599).AddHours(1));
        Assert.True(report.TestMatches > 400);
        Assert.InRange(report.TestCalibrated.ResultLogLoss, 0.5, 1.3);
        Assert.InRange(report.TestCalibrated.CalibrationError, 0, 0.2);
        Assert.Equal(5, report.TestCalibrated.Bands.Count);
        Assert.Equal(0, report.MainCards.DoubleChanceCards);
        Assert.Equal(report.TestMatches * 3, report.MainCards.FamilyDistribution.Values.Sum());
        Assert.True(report.LegacyRanking.AnyOfTop3DoubleChanceShare > 0.5);           // eski kural çifte şansı öne çıkarıyordu

        // Test penceresindeki sonuçlar değişse bile seçilen parametreler aynı kalmalı.
        var flipped = data.Select((m, i) => i >= 2000 ? m with { HomeGoals = m.AwayGoals, AwayGoals = m.HomeGoals } : m).ToList();
        var report2 = OutcomeBacktest.Run(flipped, new HashSet<int> { 140 }, At(300), At(1200), At(2000), At(2599).AddHours(1));
        Assert.Equal(JsonSerializer.Serialize(report.Parameters), JsonSerializer.Serialize(report2.Parameters));
        Assert.NotEqual(report.TestCalibrated.ResultLogLoss, report2.TestCalibrated.ResultLogLoss);
    }

    [Fact]
    public void Kalibrasyon_BeraberlikYanliligini_DagilimUzerindenDuzeltir_MarketlerTutarliKalir()
    {
        // Veri sistematik olarak bağımsız Poisson'dan fazla beraberlik içeriyor.
        var rnd = new Random(5);
        var data = Synthetic(3000, 21, i => rnd.NextDouble() < 0.33 ? (1, 1) : (Poisson(rnd, 1.5), Poisson(rnd, 1.1)));
        DateTime At(int i) => data[i].KickoffUtc;
        var report = OutcomeBacktest.Run(data, new HashSet<int> { 140 }, At(300), At(1300), At(2200), At(2999).AddHours(1));
        Assert.True(report.CalibrationApplied);
        Assert.True(report.Parameters.DrawInflation > 1.0);
        Assert.True(report.TestCalibrated.ResultLogLoss <= report.TestRaw.ResultLogLoss);
        Assert.True(Snap(1.5, 1.1, 1, report.Parameters).Checks!.Consistent);
    }

    [Fact]
    public async Task Snapshot_KesfetVeDetayAyniKayit_ModelSurumuYazilir_GirdiDegismezseYeniSatirYok()
    {
        var name = Guid.NewGuid().ToString();
        FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        using (var db = Db())
        {
            for (var i = 1; i <= 4; i++) db.Teams.Add(new Team { Id = i, Name = "Takım " + i });
            var id = 1;
            for (var d = 60; d >= 2; d--)
            {
                var h = d % 4 + 1; var a = (d + 1) % 4 + 1;
                db.Matches.Add(new Match { Id = id++, LeagueId = 140, MatchDate = now.AddDays(-d), Status = MatchStatuses.Finished, HomeTeamId = h, AwayTeamId = a, HomeScore = d % 3, AwayScore = d % 2 });
            }
            db.Matches.Add(new Match { Id = 999, LeagueId = 140, MatchDate = now.AddDays(1), Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2 });
            db.SaveChanges();
        }
        async Task<SnapshotCycleReport> Run()
        {
            using var db = Db();
            var loader = new OutcomeHistoryLoader(db);
            return await new MatchPredictionSnapshotService(db, loader, new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance),
                NullLogger<MatchPredictionSnapshotService>.Instance).RunAsync(now);
        }
        Assert.Equal(1, (await Run()).Written);
        Assert.Equal(1, (await Run()).Unchanged);                                          // aynı girdi → yeni satır yok

        using (var db = Db())
        {
            var reader = new MatchOutcomeSnapshotReader(db);
            var detail = await reader.GetCurrentAsync(999);
            var discover = (await reader.GetCurrentForMatchesAsync(new[] { 999 }))[999];
            Assert.Equal(detail.SnapshotId, discover.SnapshotId);
            Assert.Equal(JsonSerializer.Serialize(detail), JsonSerializer.Serialize(discover));
            Assert.Equal(OutcomeModelVersion.Current, detail.ModelVersion);
            Assert.NotNull(detail.ComputedAtUtc);
            Assert.Equal(1, db.MatchPredictionSnapshots.Count(s => s.MatchId == 999 && s.IsCurrent));
            Assert.Equal("Pending", (await reader.GetCurrentAsync(12345)).Status);
        }
    }

    [Fact]
    public void EskiSecimlerOkunur_YeniCizgilerSettlementaEklenir_CifteSansSettlementiBozulmaz()
    {
        Assert.Equal(OddsMarketKeys.DoubleChance1X, DecisionMarketOddsMapper.ToOddsKey("Çifte Şans (1X)"));
        Assert.Equal(OddsMarketKeys.Over25, DecisionMarketOddsMapper.ToOddsKey("2.5 Üst"));
        Assert.Equal(OddsMarketKeys.Under15, DecisionMarketOddsMapper.ToOddsKey("1.5 Alt"));
        Assert.Equal(OddsMarketKeys.Over35, DecisionMarketOddsMapper.ToOddsKey("3.5 Üst"));
        var s = new PickSettlement.FinalScore(2, 1, 1, 0);
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won, PickSettlement.Settle(OddsMarketKeys.DoubleChance1X, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won, PickSettlement.Settle(OddsMarketKeys.Over15, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Lost, PickSettlement.Settle(OddsMarketKeys.Over35, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won, PickSettlement.Settle(OddsMarketKeys.Under35, s));
        Assert.True(PickMarketGroups.Conflicts(OddsMarketKeys.Over15, OddsMarketKeys.Under15));
        Assert.False(PickMarketGroups.Conflicts(OddsMarketKeys.Over15, OddsMarketKeys.Under25));
    }
}
