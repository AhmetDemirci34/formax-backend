using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Pick = Formax.Application.Services.Outcomes.SelectivePrediction.Pick;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Tests;

/// <summary>
/// SEÇİCİ TAHMİN + İLERİYE DÖNÜK GÖLGE DEFTERİ (26.09.2026) — seçim, güven aralığı, kalibrasyon bantları, kilitli kayıt,
/// otomatik puanlama ve Model 5 gölgenin üretimi değiştirmemesi.
/// </summary>
public class SelectivePredictionTests
{
    private static readonly DateTime K = new(2026, 9, 27, 18, 45, 0, DateTimeKind.Utc);

    private static Pick P(string market, double p, bool correct, int id = 1, DateTime? at = null)
        => new(id, 39, at ?? K, market, "x", p, 0.1, 0.5, correct, 20, 20, 1);

    [Fact]
    public void Wilson_BilinenDegerler()
    {
        var (lo, hi) = SelectivePrediction.Wilson(90, 100);
        Assert.Equal(0.8256, lo, 3); Assert.Equal(0.9448, hi, 3);
        Assert.Equal((0.0, 1.0), SelectivePrediction.Wilson(0, 0));
        var (lo10, _) = SelectivePrediction.Wilson(9, 10);
        Assert.True(lo10 < 0.60); // 10 maçta 9 doğru hiçbir şey kanıtlamaz
    }

    [Fact]
    public void YetersizOrneklemde_YuzdeDoksanDenmez_EsikOgrenilmez()
    {
        var perfect49 = Enumerable.Range(0, 49).Select(i => P(MarketFamilies.DoubleChance, 0.95, true, i)).ToList();
        Assert.Null(SelectivePrediction.LearnThresholds(perfect49)[MarketFamilies.DoubleChance]);
        Assert.Null(SelectivePrediction.LearnThresholdsForward(perfect49)[MarketFamilies.DoubleChance]);
        // KG hiçbir koşulda güçlü katmana giremez.
        var btts = Enumerable.Range(0, 500).Select(i => P(MarketFamilies.BothTeamsToScore, 0.9, true, i)).ToList();
        Assert.Null(SelectivePrediction.LearnThresholds(btts)[MarketFamilies.BothTeamsToScore]);
        // Admin özeti 200 altı güçlü örneklemde başarı iddiası yapmaz.
        Assert.Contains("Yetersiz örneklem", Source("Formax.API", "Controllers", "Admin", "AdminForwardShadowController.cs"));
    }

    [Fact]
    public void EsikOgrenici_YalnizVerilenTestOncesiOrneklerle_Calisir()
    {
        // 300 örnek: p ≥ 0,90'da %96 doğru → eşik bulunur; aynı veriye test dönemi eklenmediği için sonuç sabittir.
        var pre = Enumerable.Range(0, 300).Select(i => P(MarketFamilies.DoubleChance, i < 150 ? 0.92 : 0.75, i < 150 ? i % 25 != 0 : i % 3 != 0, i)).ToList();
        var t1 = SelectivePrediction.LearnThresholds(pre);
        var t2 = SelectivePrediction.LearnThresholds(pre.ToList());
        Assert.Equal(t1[MarketFamilies.DoubleChance], t2[MarketFamilies.DoubleChance]);
        Assert.NotNull(t1[MarketFamilies.DoubleChance]);
        // Laboratuvar eşiği test öncesi örneklerle öğrenir (kaynak sözleşmesi).
        var lab = Source("tests", "Formax.Tests", "SelectivePredictionLabTests.cs");
        Assert.Contains("SelectivePrediction.LearnThresholds(prePicks)", lab);
        Assert.DoesNotContain("LearnThresholds(testPicks)", lab);
    }

    [Fact]
    public void Secim_EvDeplasmanYonu_ToplamBir_CifteSansTuretimi()
    {
        var d = ScoreDistribution.Poisson(2.2, 0.6);
        var s = SelectivePrediction.Choose(MarketFamilies.MatchResult, d, 1, 39, K, 2, 0, 20, 20, 1);
        Assert.Equal("1", s.Outcome); Assert.True(s.Correct);
        Assert.False(SelectivePrediction.Choose(MarketFamilies.MatchResult, d, 1, 39, K, 0, 2, 20, 20, 1).Correct);
        var r = ForwardPredictionLedger.Probabilities(MarketFamilies.MatchResult, d);
        Assert.Equal(1.0, r["1"] + r["X"] + r["2"], 5);
        var dc = ForwardPredictionLedger.Probabilities(MarketFamilies.DoubleChance, d);
        Assert.InRange(Math.Abs(r["1"] + r["X"] - dc["1X"]), 0, 2e-6); Assert.InRange(Math.Abs(r["X"] + r["2"] - dc["X2"]), 0, 2e-6); Assert.InRange(Math.Abs(r["1"] + r["2"] - dc["12"]), 0, 2e-6);
        var pick = SelectivePrediction.Choose(MarketFamilies.DoubleChance, d, 1, 39, K, 1, 1, 20, 20, 1);
        Assert.Equal("1X", pick.Outcome); Assert.True(pick.Correct);
        foreach (var m in new[] { MarketFamilies.TotalGoals15, MarketFamilies.TotalGoals25, MarketFamilies.TotalGoals35, MarketFamilies.BothTeamsToScore })
            Assert.InRange(SelectivePrediction.Choose(m, d, 1, 39, K, null, null, 20, 20, 1).Probability, 0.5, 1.0);
    }

    [Fact]
    public void Abstain_GucluTahminOlarakCikmaz()
    {
        var picks = new[] { P(MarketFamilies.DoubleChance, 0.97, true) };
        var thr = new Dictionary<string, double?> { [MarketFamilies.DoubleChance] = 0.84 };
        var (tier, strong) = SelectivePrediction.Tier(picks, thr, sufficient: false);
        Assert.Equal(SelectivePrediction.Tiers.Abstain, tier); Assert.Null(strong);
        Assert.Equal(SelectivePrediction.Tiers.Regular, SelectivePrediction.Tier(new[] { P(MarketFamilies.DoubleChance, 0.80, true) }, thr, true).Tier);
        Assert.Equal(SelectivePrediction.Tiers.Strongest, SelectivePrediction.Tier(picks, thr, true).Tier);
        // Eşik yoksa (1X2) güçlü katman oluşmaz.
        Assert.Equal(SelectivePrediction.Tiers.Regular, SelectivePrediction.Tier(new[] { P(MarketFamilies.MatchResult, 0.95, true) }, SelectivePrediction.ForwardThresholds, true).Tier);
    }

    [Fact]
    public void KalibrasyonBantlari_VePrecisionCoverage_DogruSayar()
    {
        var picks = new List<Pick>();
        for (var i = 0; i < 100; i++) picks.Add(P(MarketFamilies.MatchResult, 0.55, i < 55, i));
        for (var i = 0; i < 100; i++) picks.Add(P(MarketFamilies.MatchResult, 0.92, i < 80, 1000 + i));
        var bands = SelectivePrediction.CalibrationBands(picks);
        Assert.Equal(100, bands.Single(b => b.Label == "%50–59").N);
        var top = bands.Single(b => b.Label == "%90+");
        Assert.Equal(100, top.N); Assert.Equal(0.80, top.Accuracy, 4); Assert.Equal(0.12, top.CalibrationGap, 4); // aşırı güven görünür
        var pc = SelectivePrediction.PrecisionCoverage(picks);
        Assert.Equal(200, pc.Last().N);
        Assert.Equal(2, pc.First().N); // en güçlü %1 = 2 tahmin
        Assert.All(pc.Take(4), r => Assert.True(r.MinProbability >= 0.92 - 1e-9));
    }

    [Fact]
    public void DynamicElo_LaboratuvarEloIleAyni()
    {
        var rng = new Random(3);
        var h = Enumerable.Range(0, 400).Select(i => new HistoricalMatch(i + 1, K.AddDays(-400 + i / 5), 39 + (i % 2), 100 + rng.Next(20), 200 + rng.Next(20), rng.Next(4), rng.Next(3))).ToList();
        var a = OutcomeAccuracyLab.EloLogits(h, K);
        var b = new Dictionary<int, double>();
        DynamicElo.Replay(h, K, (m, x) => b[m.MatchId] = x);
        Assert.All(a, kv => Assert.Equal(kv.Value, b[kv.Key], 12));
    }

    // ═══ İleriye dönük gölge defteri ═══

    private sealed class Store
    {
        private readonly string _name = "fwd-" + Guid.NewGuid().ToString("N");
        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    }

    private static ForwardPredictionLedger.Input In(int id, DateTime kickoff, double lh = 1.6, double la = 1.0)
        => new(id, 39, kickoff, "snp-x", ScoreDistribution.Poisson(lh, la), ScoreDistribution.Poisson(lh + 0.2, la), 20, 20, 1);

    [Fact]
    public async Task Defter_KickofftanOnceKilitlenir_Duplicateyok_SonradanDegismez_RestarttaKorunur()
    {
        var st = new Store();
        var now = K.AddHours(-3);
        using (var db = st.Db()) Assert.Equal(12, await ForwardPredictionLedger.RecordAsync(db, new[] { In(1, K) }, now));
        using (var db = st.Db())
        {
            // Aynı maç ikinci kez (farklı olasılıklarla): yazılmaz, eski değerler aynen kalır.
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(1, K, 3.0, 0.2) }, now.AddHours(1)));
            // Başlamış maç ve 24 saatten uzak maç kaydedilmez.
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(2, now.AddMinutes(-1)) }, now));
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(3, now.AddHours(25)) }, now));
        }
        using (var db = st.Db()) // "restart": yeni bağlam
        {
            var rows = db.ForwardPredictionRecords.Where(r => r.MatchId == 1).ToList();
            Assert.Equal(12, rows.Count);
            Assert.Equal(2, rows.Select(r => r.ModelVersion).Distinct().Count());
            Assert.All(rows, r => { Assert.Equal(now, r.PredictionLockedAtUtc); Assert.True(r.PredictionLockedAtUtc < r.KickoffUtc); });
            var p1 = JsonSerializer.Deserialize<Dictionary<string, double>>(rows.Single(r => r.ModelVersion == OutcomeModelVersion.Current && r.Market == MarketFamilies.MatchResult).ProbabilitiesJson)!;
            Assert.Equal(ScoreDistribution.Poisson(1.6, 1.0).HomeWin, p1["1"], 5);
            Assert.Equal(rows.Count, rows.Select(r => (r.MatchId, r.ModelVersion, r.Market)).Distinct().Count());
        }
        // Kod yolu: kayıt yalnız ekleme yapar, olasılık alanlarını güncellemez.
        var src = Source("Formax.Infrastructure", "Outcomes", "ForwardPredictionLedger.cs");
        Assert.DoesNotContain(".ProbabilitiesJson =", src.Replace("ProbabilitiesJson = JsonSerializer.Serialize(Probabilities(", ""));
    }

    [Fact]
    public async Task Defter_SonucGelinceOtomatikPuanlanir_DuzeltmedeDenetimliYenidenPuanlanir()
    {
        var st = new Store();
        using (var db = st.Db())
        {
            db.Matches.Add(new Match { Id = 1, LeagueId = 39, MatchDate = K, Status = MatchStatuses.NotStarted, HomeTeamId = 10, AwayTeamId = 11 });
            db.Matches.Add(new Match { Id = 2, LeagueId = 39, MatchDate = K, Status = MatchStatuses.NotStarted, HomeTeamId = 12, AwayTeamId = 13 });
            db.SaveChanges();
            await ForwardPredictionLedger.RecordAsync(db, new[] { In(1, K), In(2, K) }, K.AddHours(-2));
        }
        using (var db = st.Db())
        {
            var m1 = db.Matches.Single(m => m.Id == 1); m1.Status = MatchStatuses.Finished; m1.HomeScore = 2; m1.AwayScore = 0;
            var m2 = db.Matches.Single(m => m.Id == 2); m2.Status = MatchStatuses.Postponed;
            db.SaveChanges();
            var (scored, rescored, voided) = await ForwardPredictionLedger.ScoreAsync(db, K.AddHours(3));
            Assert.Equal(12, scored); Assert.Equal(0, rescored); Assert.Equal(12, voided);
        }
        using (var db = st.Db())
        {
            var r = db.ForwardPredictionRecords.Single(x => x.MatchId == 1 && x.ModelVersion == OutcomeModelVersion.Current && x.Market == MarketFamilies.MatchResult);
            Assert.Equal("1", r.ActualOutcome); Assert.True(r.Correct);
            var p = JsonSerializer.Deserialize<Dictionary<string, double>>(r.ProbabilitiesJson)!;
            Assert.Equal(Math.Round(-Math.Log(p["1"]), 6), r.LogLoss!.Value, 6);
            Assert.True(db.ForwardPredictionRecords.Where(x => x.MatchId == 2).All(x => x.ActualOutcome == "Void" && x.Correct == null));
            // Resmî düzeltme: 2-0 → 0-1.
            var m1 = db.Matches.Single(m => m.Id == 1); m1.HomeScore = 0; m1.AwayScore = 1; db.SaveChanges();
            var (_, rescored, _) = await ForwardPredictionLedger.ScoreAsync(db, K.AddHours(5));
            Assert.Equal(12, rescored); // iki model × altı market
        }
        using (var db = st.Db())
        {
            var r = db.ForwardPredictionRecords.Single(x => x.MatchId == 1 && x.ModelVersion == OutcomeModelVersion.Current && x.Market == MarketFamilies.MatchResult);
            Assert.Equal("2", r.ActualOutcome); Assert.False(r.Correct); Assert.Equal(1, r.RescoreCount);
            Assert.Contains("\"oldHome\":2", r.ScoreAuditJson);
            // Değişmeyen sonuç tekrar puanlanmaz.
            var (s, re, v) = await ForwardPredictionLedger.ScoreAsync(db, K.AddHours(6));
            Assert.Equal((0, 0, 0), (s, re, v));
        }
    }

    [Fact]
    public async Task Model5Golge_UretimSnapshotiniDegistirmez_UefaVeLiglerArasiUygulanmaz()
    {
        var d = ScoreDistribution.Poisson(1.5, 1.1);
        Assert.Same(d, Model5Shadow.Predict(d, 2, false, 1.0));      // UCL
        Assert.Same(d, Model5Shadow.Predict(d, 848, false, 1.0));    // UECL
        Assert.Same(d, Model5Shadow.Predict(d, 39, true, 1.0));      // ligler arası
        Assert.NotSame(d, Model5Shadow.Predict(d, 39, false, 1.0));
        Assert.Equal(1.0, Model5Shadow.Predict(d, 39, false, 1.0).HomeWin + Model5Shadow.Predict(d, 39, false, 1.0).Draw + Model5Shadow.Predict(d, 39, false, 1.0).AwayWin, 9);

        // Uçtan uca: snapshot turu gölge kaydı üretir; yayımlanan snapshot 4.0 olasılıklarıyla aynıdır, gölgeyi taşımaz.
        var w = new World();
        using (var db = w.Db()) Assert.Equal(1, (await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-3))).Written);
        using (var db = w.Db())
        {
            var snap = await new MatchOutcomeSnapshotReader(db).GetCurrentAsync(World.MatchId);
            var raw = db.MatchPredictionSnapshots.Single(s => s.MatchId == World.MatchId && s.IsCurrent);
            Assert.DoesNotContain("5-shadow", raw.PayloadJson);
            var rows = db.ForwardPredictionRecords.Where(r => r.MatchId == World.MatchId).ToList();
            Assert.Equal(12, rows.Count);
            Assert.Equal(raw.SnapshotId, rows.First().SnapshotId);
            var r40 = JsonSerializer.Deserialize<Dictionary<string, double>>(rows.Single(r => r.ModelVersion == OutcomeModelVersion.Current && r.Market == MarketFamilies.MatchResult).ProbabilitiesJson)!;
            var payload = JsonSerializer.Deserialize<OutcomeSnapshotDto>(raw.PayloadJson)!;
            var home = payload.Families.First(f => f.Family == OutcomeFamilies.Result).Items.First(i => i.MarketKey == OddsMarketKeys.Ms1).CalibratedProbability;
            Assert.InRange(Math.Abs(home - r40["1"]), 0, 0.0006); // yük 4, defter 6 basamak
            Assert.Equal(snap.SnapshotId, raw.SnapshotId);
        }
        // İkinci tur: aynı girdi → yeni snapshot yok, gölge kaydı tekrarlanmaz.
        using (var db = w.Db()) Assert.Equal(0, (await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-2))).Written);
        using (var db = w.Db()) Assert.Equal(12, db.ForwardPredictionRecords.Count());
    }

    [Fact]
    public void ApiFootball_VeDisIstek_Yok()
    {
        foreach (var f in new[]
                 {
                     Source("Formax.Application", "Services", "Outcomes", "SelectivePrediction.cs"),
                     Source("Formax.Infrastructure", "Outcomes", "ForwardPredictionLedger.cs"),
                     Source("Formax.API", "Controllers", "Admin", "AdminForwardShadowController.cs")
                 })
        {
            Assert.DoesNotContain("HttpClient", f); Assert.DoesNotContain("ApiFootball", f); Assert.DoesNotContain("IOfficialContentFetcher", f);
        }
        // Kullanıcı uçları gölge defterini okumaz.
        Assert.DoesNotContain("ForwardPrediction", Source("Formax.API", "Controllers", "MatchOutcomesController.cs"));
    }

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    private sealed class World
    {
        private readonly string _name = "fwd-snap-" + Guid.NewGuid().ToString("N");
        public static readonly DateTime Kickoff = new(2026, 9, 27, 18, 45, 0, DateTimeKind.Utc);
        public const int MatchId = 880001;

        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        public World()
        {
            using var db = Db();
            for (var i = 1; i <= 8; i++) db.Teams.Add(new Team { Id = i, Name = "T" + i });
            var rnd = new Random(4); var id = 1;
            for (var d = 220; d >= 3; d -= 2)
                for (var k = 0; k < 2; k++)
                {
                    var h = rnd.Next(1, 9); var a = rnd.Next(1, 9); if (a == h) a = h % 8 + 1;
                    db.Matches.Add(new Match { Id = id++, LeagueId = 135, League = "Serie A", MatchDate = Kickoff.AddDays(-d).AddHours(k), Status = MatchStatuses.Finished, HomeTeamId = h, AwayTeamId = a, HomeScore = rnd.Next(4), AwayScore = rnd.Next(3) });
                }
            db.Matches.Add(new Match { Id = MatchId, LeagueId = 135, League = "Serie A", MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2 });
            var p = new OutcomeModelParameters { MaxSupportedProbability = 0.99, EloConflictThreshold = 1.0 };
            db.PredictionModelRuns.Add(new PredictionModelRun { RunId = "run-fwd", ModelVersion = OutcomeModelVersion.Current, Status = "Accepted", StartedAtUtc = Kickoff.AddDays(-4), CompletedAtUtc = Kickoff.AddDays(-4), ParametersJson = JsonSerializer.Serialize(p), MetricsJson = "{}", TestMatches = 400 });
            db.LeaguePredictionEligibilities.Add(new LeaguePredictionEligibility { RunId = "run-fwd", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = EligibilityPolicy.Version, LeagueId = 135, Status = PredictionEligibilities.Enabled, ReasonsJson = "[]", MetricsJson = "{}", EvaluatedAtUtc = Kickoff.AddDays(-4) });
            foreach (var f in MarketFamilies.All)
                db.LeagueMarketEligibilities.Add(new LeagueMarketEligibility { RunId = "run-fwd", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = MarketEligibilityPolicy.Version, LeagueId = 135, Family = f, TestMatches = 400, Status = MarketEligibilityStatuses.Eligible, ReasonsJson = "[]", MetricsJson = "{}", EvaluatedAtUtc = Kickoff.AddDays(-4) });
            db.SaveChanges();
        }

        public MatchPredictionSnapshotService Snapshots(FormaxDbContext db)
        {
            var loader = new OutcomeHistoryLoader(db);
            return new MatchPredictionSnapshotService(db, loader, new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance), NullLogger<MatchPredictionSnapshotService>.Instance);
        }
    }
}
