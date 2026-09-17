using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.MatchAnalysis;
using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Tests;

/// <summary>
/// TAHMİN GÜVENİLİRLİĞİ (17.09.2026) — lig uygunluğu, ligler arası ortak güç ölçeği, maç kapıları, snapshot yenileme kuyruğu,
/// değişim kapısı, canlı karne, analiz–kart çelişki kapısı, UEFA sonuç kaynağı. Gerçek internet YOK; DB InMemory.
/// </summary>
public class PredictionReliabilityTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static int Poisson(Random r, double l) { var L = Math.Exp(-l); var k = 0; var p = 1.0; do { k++; p *= r.NextDouble(); } while (p > L); return k - 1; }

    // ═══ 1. UYGUNLUK POLİTİKASI ═══════════════════════════════════════════════

    private static GroupMetrics Good(int n = 400) => new()
    {
        Matches = n, LogLossDiff = -0.05, LogLossDiffCiHigh = -0.02, SignificantlyBetter = true, BetterThanBaseline = true,
        CalibrationError = 0.015, HomeBias = 0.01, DrawBias = -0.01, Over25LogLoss = 0.68, BaselineOver25LogLoss = 0.69,
        BttsLogLoss = 0.69, BaselineBttsLogLoss = 0.69, FinishedLast60Days = 30
    };

    [Fact]
    public void Uygunluk_Enabled_Limited_Disabled_KurallariSurumlu()
    {
        var g = Good(); EligibilityPolicy.Decide(g);
        Assert.Equal(PredictionEligibilities.Enabled, g.Eligibility);

        var small = Good(250); EligibilityPolicy.Decide(small);
        Assert.Equal(PredictionEligibilities.Limited, small.Eligibility);
        Assert.Contains("LIMITED_SAMPLE_BELOW_300", small.EligibilityReasons);

        var notSig = Good(); notSig.SignificantlyBetter = false; notSig.LogLossDiffCiHigh = 0.01; EligibilityPolicy.Decide(notSig);
        Assert.Equal(PredictionEligibilities.Limited, notSig.Eligibility);

        var drawBias = Good(); drawBias.DrawBias = 0.05; EligibilityPolicy.Decide(drawBias);
        Assert.Contains("LIMITED_DRAW_BIAS", drawBias.EligibilityReasons);

        var goals = Good(); goals.Over25LogLoss = 0.71; EligibilityPolicy.Decide(goals);
        Assert.Contains("LIMITED_GOALS_WORSE_THAN_BASELINE", goals.EligibilityReasons);

        var worse = Good(); worse.LogLossDiff = 0.01; EligibilityPolicy.Decide(worse);
        Assert.Equal(PredictionEligibilities.Disabled, worse.Eligibility);           // tabandan kötü lig ASLA Enabled değil

        var tiny = Good(60); EligibilityPolicy.Decide(tiny);
        Assert.Equal(PredictionEligibilities.Disabled, tiny.Eligibility);

        var broken = Good(); broken.CalibrationError = 0.08; EligibilityPolicy.Decide(broken);
        Assert.Equal(PredictionEligibilities.Disabled, broken.Eligibility);
        Assert.Equal("eligibility-1", EligibilityPolicy.Version);
    }

    [Fact]
    public void Uygunluk_MacKapilari_SertKapiDisabled_CiktiKapisiLimited_LigKarariniAsamaz()
    {
        Assert.Equal(PredictionEligibilities.Enabled, OutcomeSnapshotBuilder.Combine("Enabled", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()).Eligibility);
        Assert.Equal(PredictionEligibilities.Disabled, OutcomeSnapshotBuilder.Combine("Enabled", Array.Empty<string>(), new[] { "INSUFFICIENT_SAMPLE" }, Array.Empty<string>()).Eligibility);
        Assert.Equal(PredictionEligibilities.Disabled, OutcomeSnapshotBuilder.Combine("Enabled", Array.Empty<string>(), new[] { "CROSS_LEAGUE_UNLINKED" }, Array.Empty<string>()).Eligibility);
        Assert.Equal(PredictionEligibilities.Disabled, OutcomeSnapshotBuilder.Combine("Enabled", Array.Empty<string>(), new[] { "TEAM_LEAGUE_UNKNOWN" }, Array.Empty<string>()).Eligibility);
        Assert.Equal(PredictionEligibilities.Limited, OutcomeSnapshotBuilder.Combine("Enabled", Array.Empty<string>(), Array.Empty<string>(), new[] { "OUTLIER_PROBABILITY" }).Eligibility);
        Assert.Equal(PredictionEligibilities.Limited, OutcomeSnapshotBuilder.Combine("Limited", new[] { "LIMITED_DRAW_BIAS" }, Array.Empty<string>(), Array.Empty<string>()).Eligibility);
        Assert.Equal(PredictionEligibilities.Disabled, OutcomeSnapshotBuilder.Combine(null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()).Eligibility);
    }

    private static OutcomeExpectation E(double lh, double la, double coverage = 1, int n = 20)
        => new(lh, la, 1.45, 1.15, n, n, coverage, n >= 4, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);

    [Fact]
    public void KullaniciGorunumu_LimitedDisabledMactaYuzdeTasinmaz_EnabledAynen()
    {
        var full = OutcomeSnapshotBuilder.Build(7, OutcomePredictor.Predict(E(1.6, 1.1), 140, new OutcomeModelParameters()), "Ev", "Dep");
        full.SnapshotId = "snp-x";
        full.PredictionEligibility = PredictionEligibilities.Limited;
        var user = OutcomeSnapshotBuilder.ForUser(full);
        Assert.Equal("NotEligible", user.Status);
        Assert.Empty(user.MainCards);
        Assert.Empty(user.Families);
        Assert.Empty(user.TopScores);
        Assert.Null(user.ExpectedHomeGoals);
        Assert.Equal("snp-x", user.SnapshotId);
        Assert.Equal(OutcomeSnapshotBuilder.NotEligibleNotice, user.Notice);
        Assert.Equal("Bu maç için güvenilir AI beklentisi oluşturacak yeterli doğrulanmış veri bulunmuyor.", user.Notice);

        full.PredictionEligibility = PredictionEligibilities.Enabled;
        Assert.Same(full, OutcomeSnapshotBuilder.ForUser(full));
    }

    [Fact]
    public void Matematik_TekDagilim_AnaKartta1X_X2_12Yok_UcFarkliAile_OranGirdiDegil()
    {
        var rnd = new Random(3);
        for (var i = 0; i < 300; i++)
        {
            var s = OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(0.3 + rnd.NextDouble() * 2.8, 0.3 + rnd.NextDouble() * 2.5), 140, new OutcomeModelParameters { TotalGoalShrink = 0.5 }), "Ev", "Dep");
            Assert.True(s.Checks!.Consistent);
            Assert.DoesNotContain(s.MainCards, c => c.MarketKey != null && OutcomeFamilies.IsCompound(c.MarketKey));
            Assert.Equal(3, s.MainCards.Select(c => c.Family).Distinct().Count());
            var r = s.Families[0].Items;
            Assert.Equal(100, r.Sum(x => x.Probability));
        }
        // Oran (bookmaker) tahmin zincirine girmez: kurucu ve servis imzalarında/kaynağında oran yok.
        Assert.DoesNotContain(typeof(OutcomeSnapshotBuilder).GetMethod("Build")!.GetParameters(), p => p.ParameterType.Name.Contains("Odds"));
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs"));
        Assert.DoesNotContain("MatchMarketOdds", src);
        Assert.DoesNotContain("IOddsRepository", src);
    }

    // ═══ 2. LİGLER ARASI ORTAK GÜÇ ÖLÇEĞİ ═════════════════════════════════════

    /// <summary>
    /// Sentetik iki lig: güçlü lig (900) ve zayıf lig (901); ligler arası organizasyon 902. Zayıf ligin lideri kendi liginde baskın
    /// (Omonia benzeri), güçlü ligin orta sıra takımı (Celta benzeri) kendi liginde vasat. Gerçek güç: lider 0,75, orta sıra 1,0.
    /// </summary>
    private static (List<HistoricalMatch> History, int Omonia, int Celta, DateTime End) CrossLeagueWorld(int seed, int crossMatches = 160)
    {
        var rnd = new Random(seed);
        var start = new DateTime(2024, 1, 6, 15, 0, 0, DateTimeKind.Utc);
        var strong = Enumerable.Range(0, 10).Select(i => 0.8 + 0.04 * i).ToArray();       // 101..110
        var weak = Enumerable.Range(0, 10).Select(i => 0.35 + 0.02 * i).ToArray();        // 201..210
        weak[9] = 0.75;                                                                    // 210 = "Omonia"
        const int celta = 105;                                                             // güç 0,96 ≈ orta sıra
        double S(int team) => team < 200 ? strong[team - 101] : weak[team - 201];
        var list = new List<HistoricalMatch>();
        var id = 1;
        var t = start;
        // 4 yıl (480 tur): ligler arası organizasyonda takım başına yıllık maç gerçek UEFA gibi düşük kalır (lig sayılmaz).
        const int rounds = 480;
        var crossEvery = Math.Max(1, (rounds - 10) / Math.Max(1, crossMatches / 2));
        for (var round = 0; round < rounds; round++)
        {
            t = t.AddDays(3);
            foreach (var (lg, baseId) in new[] { (900, 101), (901, 201) })
                for (var k = 0; k < 5; k++)
                {
                    var h = baseId + rnd.Next(10); var a = baseId + rnd.Next(10); if (a == h) a = baseId + (h - baseId + 1) % 10;
                    list.Add(new HistoricalMatch(id++, t.AddHours(k), lg, h, a, Poisson(rnd, 1.45 * S(h) / S(a)), Poisson(rnd, 1.15 * S(a) / S(h))));
                }
            if (round >= 10 && (round - 10) % crossEvery == 0 && list.Count(m => m.LeagueId == 902) < crossMatches)
                for (var k = 0; k < 2; k++)
                {
                    var h = rnd.Next(2) == 0 ? 101 + rnd.Next(10) : 201 + rnd.Next(10);
                    var a = h < 200 ? 201 + rnd.Next(10) : 101 + rnd.Next(10);
                    list.Add(new HistoricalMatch(id++, t.AddHours(10 + k), 902, h, a, Poisson(rnd, 1.45 * S(h) / S(a)), Poisson(rnd, 1.15 * S(a) / S(h))));
                }
        }
        return (list.OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList(), 210, celta, t.AddDays(3));
    }

    [Fact]
    public void LiglerArasi_OmoniaCeltaRegresyonu_OrtakOlcekZayifLigLideriniFavoriYapmaz_EskiModelYapiyordu()
    {
        var (history, omonia, celta, end) = CrossLeagueWorld(11, crossMatches: 80);
        var catalog = CompetitionCatalog.Build(history);
        Assert.True(catalog.IsLeague(900) && catalog.IsLeague(901));
        Assert.False(catalog.IsLeague(902));

        var p = new OutcomeModelParameters { TotalGoalShrink = 1, UncertaintyMix = 0 };
        var v3 = new OutcomeRatingModel(p, catalog);
        foreach (var m in history) v3.Update(m);
        v3.RefitLeagueStrengths(end);
        var e3 = v3.Expect(902, omonia, celta, end);
        var d3 = OutcomePredictor.Predict(e3, 902, p).Calibrated;

        var legacyP = OutcomeModelParameters.Legacy();
        var v2 = new OutcomeRatingModel(legacyP);
        foreach (var m in history) v2.Update(m);
        var d2 = OutcomePredictor.Predict(v2.Expect(902, omonia, celta, end), 902, legacyP).Calibrated;

        Assert.True(e3.CrossLeague);
        Assert.Equal(901, e3.HomeLeagueId);
        Assert.Equal(900, e3.AwayLeagueId);
        Assert.True(e3.AwayLeagueStrength > e3.HomeLeagueStrength + 0.3, $"S güçlü={e3.AwayLeagueStrength} zayıf={e3.HomeLeagueStrength}");
        Assert.True(d2.HomeWin > d2.AwayWin, "eski model zayıf lig liderini favori görmeliydi (hata tekrarı)");
        // Gerçek güçlerden (0,75 vs 0,96) üretilen doğru dağılıma yeni model daha yakın; favori tarafı doğru (deplasman).
        var truth = ScoreDistribution.Poisson(1.45 * 0.75 / 0.96, 1.15 * 0.96 / 0.75);
        Assert.True(Math.Abs(d3.HomeWin - truth.HomeWin) < Math.Abs(d2.HomeWin - truth.HomeWin), $"yeni {d3.HomeWin:P1} eski {d2.HomeWin:P1} gerçek {truth.HomeWin:P1}");
        Assert.True(d3.AwayWin > d3.HomeWin, $"yeni ev {d3.HomeWin:P1} dep {d3.AwayWin:P1}");
    }

    [Fact]
    public void LiglerArasi_50denFazlaGecmisMac_YeniModelLogLossuEskiModeldenIyi_KapsamRaporlanir()
    {
        var (history, _, _, end) = CrossLeagueWorld(21, crossMatches: 220);
        var catalog = CompetitionCatalog.Build(history);
        var test = history[(int)(history.Count * 0.6)].KickoffUtc;
        var cal = history[(int)(history.Count * 0.4)].KickoffUtc;
        var eval = history[(int)(history.Count * 0.2)].KickoffUtc;
        var r = OutcomeBacktest.Run(history, catalog, new HashSet<int> { 900, 901, 902 }, eval, cal, test, end, end, compareLegacy: true);
        Assert.True(r.CrossLeague.ComparedMatches >= 50, $"karşılaştırılan ligler arası maç {r.CrossLeague.ComparedMatches}");
        Assert.True(r.CrossLeague.CurrentModel!.ResultLogLoss < r.CrossLeague.PreviousModel!.ResultLogLoss,
            $"yeni {r.CrossLeague.CurrentModel.ResultLogLoss} eski {r.CrossLeague.PreviousModel.ResultLogLoss}");
        Assert.NotNull(r.TestPreviousModel);
        Assert.Contains(r.LeagueEligibility, g => g.LeagueId == 902);
    }

    [Fact]
    public void LiglerArasi_BaglantisizLig_ve_LigiBilinmeyenTakim_YuzdeYayimlanmaz()
    {
        var (history, _, celta, end) = CrossLeagueWorld(5);
        // 903: kendi içinde oynayan ama hiç ligler arası maçı olmayan lig; 999: hiç lig maçı olmayan takım.
        var rnd = new Random(1);
        var extra = new List<HistoricalMatch>();
        var id = 900_000;
        for (var i = 0; i < 200; i++)
        {
            var h = 301 + rnd.Next(8); var a = 301 + (h - 301 + 1 + rnd.Next(7)) % 8;
            extra.Add(new HistoricalMatch(id++, history[0].KickoffUtc.AddDays(i), 903, h, a, rnd.Next(3), rnd.Next(3)));
        }
        var all = history.Concat(extra).OrderBy(m => m.KickoffUtc).ToList();
        var model = new OutcomeRatingModel(new OutcomeModelParameters(), CompetitionCatalog.Build(all));
        foreach (var m in all) model.Update(m);
        model.RefitLeagueStrengths(end);
        var unlinked = model.Expect(902, 301, celta, end);
        Assert.False(unlinked.Sufficient);
        Assert.Contains("CROSS_LEAGUE_UNLINKED", unlinked.GateReasons);
        var unknown = model.Expect(902, 999, celta, end);
        Assert.Contains("TEAM_LEAGUE_UNKNOWN", unknown.GateReasons);
    }

    [Fact]
    public void CiktiKapisi_KanitsizAsiriOlasilik_ve_BagimsizEloCelişkisi_Limited()
    {
        var p = new OutcomeModelParameters { MaxSupportedProbability = 0.65, EloConflictThreshold = 0.2, UncertaintyMix = 0 };
        var strongHome = E(3.2, 0.4) with { EloHomeExpectation = 0.9 };
        var gates = OutcomePredictor.OutputGates(OutcomePredictor.Predict(strongHome, 140, p), p);
        Assert.Contains("OUTLIER_PROBABILITY", gates);
        var conflict = E(2.0, 0.8) with { EloHomeExpectation = 0.2 };
        Assert.Contains("RATING_DIRECTION_CONFLICT", OutcomePredictor.OutputGates(OutcomePredictor.Predict(conflict, 140, p), p));
        var fine = E(1.4, 1.2) with { EloHomeExpectation = 0.55 };
        Assert.Empty(OutcomePredictor.OutputGates(OutcomePredictor.Predict(fine, 140, p), p));
    }

    // ═══ 3. YANLILIK VE KALİBRASYON ═══════════════════════════════════════════

    [Fact]
    public void Yanlilik_EvBeraberlikOlculur_BeraberlikArgmaxYapisiRaporlanir_ToplamGolDaraltmasiTekMatris()
    {
        var rnd = new Random(8);
        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var str = Enumerable.Range(0, 21).Select(_ => 0.7 + rnd.NextDouble() * 0.6).ToArray();
        var data = new List<HistoricalMatch>();
        for (var i = 0; i < 3000; i++)
        {
            var h = rnd.Next(1, 21); var a = rnd.Next(1, 21); if (a == h) a = h % 20 + 1;
            data.Add(new HistoricalMatch(i + 1, start.AddHours(i * 9), 140, h, a, Poisson(rnd, 1.4 * str[h] / str[a]), Poisson(rnd, 1.1 * str[a] / str[h])));
        }
        DateTime At(int i) => data[i].KickoffUtc;
        var r = OutcomeBacktest.Run(data, new HashSet<int> { 140 }, At(300), At(1300), At(2200), At(2999).AddHours(1));
        Assert.True(Math.Abs(r.Bias.MeanPredictedHome - r.Bias.ActualHomeRate) < 0.05);
        Assert.True(Math.Abs(r.Bias.MeanPredictedDraw - r.Bias.ActualDrawRate) < 0.05);
        Assert.Equal(5, r.Bias.DrawCalibration.Count);
        Assert.True(r.Bias.BalancedMatches > 0);
        Assert.Contains("BİR KEZ", r.Bias.HomeAdvantageStructure);
        var s = OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(2.2, 0.9), 140, r.Parameters), "Ev", "Dep");
        Assert.True(s.Checks!.Consistent);
    }

    [Fact]
    public void ZamansalSizinti_GelecekSonuclarDegisseBile_KesimAnindakiBeklentiAyni()
    {
        var (history, omonia, celta, end) = CrossLeagueWorld(2);
        var cut = history[history.Count / 2].KickoffUtc;
        var catalog = CompetitionCatalog.Build(history);
        OutcomeExpectation At(IEnumerable<HistoricalMatch> h)
        {
            var model = new OutcomeRatingModel(new OutcomeModelParameters(), catalog);
            foreach (var m in h) { if (m.KickoffUtc >= cut) break; model.Update(m); }
            return model.Expect(902, omonia, celta, cut);
        }
        var a = At(history);
        var b = At(history.Select(m => m.KickoffUtc >= cut ? m with { HomeGoals = m.AwayGoals + 3 } : m));
        Assert.Equal(a.LambdaHome, b.LambdaHome);
        Assert.Equal(a.HomeLeagueStrength, b.HomeLeagueStrength);
    }

    // ═══ 4. DEĞİŞİM KAPISI ════════════════════════════════════════════════════

    private static OutcomeSnapshotDto Snap(OutcomeExpectation e, OutcomeModelParameters p, string run = "run-1")
    {
        var s = OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(e, 140, p), "Ev", "Dep");
        s.CalibrationRunId = run;
        s.PredictionEligibility = PredictionEligibilities.Enabled;
        return s;
    }

    [Fact]
    public void DegisimKapisi_AciklanamayanBuyukDegisim_NeedsReview_ModelGuncellemesiDenetlenirAmaYayimlanir()
    {
        var p = new OutcomeModelParameters { UncertaintyMix = 0 };
        var before = Snap(E(1.3, 1.2), p);
        var nextE = E(2.6, 0.7);
        var after = Snap(nextE, p);
        var audit = OutcomeChangeGate.Evaluate(before, after, nextE, 140, p, newFinishedMatches: 0, "OfficialLineup", "official:x", DateTime.UtcNow, new[] { "OfficialLineup:Verified" });
        Assert.Equal("NeedsReview", audit.Decision);
        Assert.Equal("UNEXPLAINED_PROBABILITY_CHANGE", audit.DecisionReason);
        Assert.Equal(0, audit.ValidatedImpactAllowance);                 // doğrulanmış oyuncu etkisi yok → kadro yüzdeyi oynatamaz
        Assert.Contains(audit.Lines, l => l.Exceeded && l.Market == "Ev Sahibi Kazanır");

        var small = Snap(E(1.33, 1.19), p);
        Assert.Equal("Published", OutcomeChangeGate.Evaluate(before, small, E(1.33, 1.19), 140, p, 1, "Periodic", null, DateTime.UtcNow, Array.Empty<string>()).Decision);

        var modelUpdate = Snap(nextE, p, run: "run-2");
        var mu = OutcomeChangeGate.Evaluate(before, modelUpdate, nextE, 140, p, 0, "ModelUpdate", null, DateTime.UtcNow, Array.Empty<string>());
        Assert.Equal("Published", mu.Decision);
        Assert.Equal("MODEL_OR_CALIBRATION_UPDATE", mu.DecisionReason);
        Assert.NotEmpty(mu.Lines);

        // Organizasyon gol tabanı değiştiyse (yeni turnuva sonuçları) gol marketlerindeki kayma açıklanmış sayılır — Juventus–NEC regresyonu.
        var baseBefore = Snap(E(1.6, 1.2), p);
        baseBefore.Strength = new OutcomeStrengthDto { LeagueHome = 1.60, LeagueAway = 1.25 };
        var shiftedE = new OutcomeExpectation(1.45, 1.09, 1.45, 1.13, 20, 20, 1, true, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);
        var baseAfter = Snap(shiftedE, p);
        baseAfter.Strength = new OutcomeStrengthDto { LeagueHome = 1.45, LeagueAway = 1.13 };
        // Taban bilgisi yok ve organizasyonda yeni maç yoksa aynı kayma açıklanamaz.
        Assert.Equal("NeedsReview", OutcomeChangeGate.Evaluate(baseBefore, Snap(shiftedE, p), shiftedE, 140, p, 0, "Periodic", null, DateTime.UtcNow, Array.Empty<string>()).Decision);
        var explained = OutcomeChangeGate.Evaluate(baseBefore, baseAfter, shiftedE, 140, p, 0, "Periodic", null, DateTime.UtcNow, new[] { "NewCompetitionMatches:9" }, 9);
        Assert.Equal("Published", explained.Decision);
        Assert.True(explained.CompetitionBaseLogShift > 0.08);

        // Sınır tek sabit değil: daha çok yeni maç ve düşük kapsam daha geniş izin verir.
        var wide = OutcomeChangeGate.Evaluate(before, small, E(1.33, 1.19, coverage: 0.5), 140, p, 6, "Periodic", null, DateTime.UtcNow, Array.Empty<string>());
        var narrow = OutcomeChangeGate.Evaluate(before, small, E(1.33, 1.19), 140, p, 0, "Periodic", null, DateTime.UtcNow, Array.Empty<string>());
        Assert.True(wide.Lines.First().Allowed > narrow.Lines.First().Allowed);
    }

    // ═══ 5. SNAPSHOT YENİLEME ZİNCİRİ (uçtan uca, InMemory) ═══════════════════

    private sealed class World
    {
        private readonly string _name = "pred-" + Guid.NewGuid().ToString("N");
        public static readonly DateTime Kickoff = new(2026, 9, 11, 18, 45, 0, DateTimeKind.Utc);
        public const int MatchId = 15383;

        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        public World(string eligibility = PredictionEligibilities.Enabled)
        {
            using var db = Db();
            var names = new[] { "Venezia", "Fiorentina", "Monza", "Sassuolo", "Lecce", "Parma", "Empoli", "Cagliari" };
            for (var i = 0; i < names.Length; i++) db.Teams.Add(new Team { Id = i + 1, Name = names[i] });
            var rnd = new Random(4);
            var id = 1;
            for (var d = 220; d >= 3; d -= 2)
                for (var k = 0; k < 2; k++)
                {
                    var h = rnd.Next(1, 9); var a = rnd.Next(1, 9); if (a == h) a = h % 8 + 1;
                    db.Matches.Add(new Match { Id = id++, LeagueId = 135, League = "Serie A", MatchDate = Kickoff.AddDays(-d).AddHours(k), Status = MatchStatuses.Finished, HomeTeamId = h, AwayTeamId = a, HomeScore = rnd.Next(4), AwayScore = rnd.Next(3) });
                }
            db.Matches.Add(new Match { Id = MatchId, ExternalMatchId = "1550126", LeagueId = 135, League = "Serie A", MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2 });
            var p = new OutcomeModelParameters { MaxSupportedProbability = 0.99, EloConflictThreshold = 1.0 };
            db.PredictionModelRuns.Add(new PredictionModelRun
            {
                RunId = "run-test", ModelVersion = OutcomeModelVersion.Current, Status = "Accepted", StartedAtUtc = Kickoff.AddDays(-1), CompletedAtUtc = Kickoff.AddDays(-1),
                ParametersJson = JsonSerializer.Serialize(p), MetricsJson = "{}", TestMatches = 400
            });
            db.LeaguePredictionEligibilities.Add(new LeaguePredictionEligibility
            {
                RunId = "run-test", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = EligibilityPolicy.Version, LeagueId = 135, Status = eligibility,
                ReasonsJson = "[]", MetricsJson = "{}", EvaluatedAtUtc = Kickoff.AddDays(-1)
            });
            db.SaveChanges();
        }

        public MatchPredictionSnapshotService Snapshots(FormaxDbContext db)
        {
            var loader = new OutcomeHistoryLoader(db);
            return new MatchPredictionSnapshotService(db, loader, new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance),
                NullLogger<MatchPredictionSnapshotService>.Instance);
        }

        public PredictionRecomputeWorker Worker(FormaxDbContext db) => new(db, Snapshots(db), NullLogger<PredictionRecomputeWorker>.Instance);

        public async Task<RecomputeCycleReport> Tick(DateTime now) { using var db = Db(); return await Worker(db).RunOnceAsync(now); }
    }

    private static OfficialLineupSide Side(string team)
        => new(team, "4-3-3", Enumerable.Range(1, 11).Select(i => new OfficialLineupPlayer($"{team} P{i}", i, "M", false)).ToList(),
            Enumerable.Range(12, 5).Select(i => new OfficialLineupPlayer($"{team} B{i}", i, "M", false)).ToList(), "Coach");

    [Fact]
    public async Task Kadro_GeldigindeOtomatikYenileme_Debounce_RestartSonrasiDevam_EskiSnapshotKorunur_IkiEkranYeniSnapshot()
    {
        var w = new World();
        using (var db = w.Db()) Assert.Equal(1, (await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-3))).Written);
        string first;
        using (var db = w.Db()) first = (await new MatchOutcomeSnapshotReader(db).GetCurrentAsync(World.MatchId)).SnapshotId!;
        using (var db = w.Db()) Assert.Equal("Available", (await new MatchOutcomeSnapshotReader(db).GetCurrentAsync(World.MatchId)).Status);

        // Resmî kadro NORMAL toplayıcı yoluyla yazılır → aynı işlemde yenileme isteği.
        var env = new OfficialLineupTests.Env();
        // Env kendi DB'sini kurar; zinciri aynı dünyada koşmak için toplayıcıyı bu dünyanın DB'sine bağla.
        var source = new OfficialLineupTests.FakeSource();
        source.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "sdp-vf", null, "Venezia", "Fiorentina", World.Kickoff, OfficialMatchStatuses.Scheduled, null, null, "UPCOMING"));
        source.Next = () => new OfficialLineupDocument(OfficialSourceRegistry.SerieASdp, "m1", "https://api-sdp.legaseriea.it/v1/x/lineups", "hash-1", null, Side("Venezia"), Side("Fiorentina"));
        async Task Collect(DateTime now)
        {
            using var db = w.Db();
            await new OfficialLineupCollector(db, new IOfficialCompetitionSource[] { source }, new OfficialLineupTests.NoopFetcher(),
                new Formax.Infrastructure.Notifications.MatchNotificationDispatcher(db, env.Delivery, NullLogger<Formax.Infrastructure.Notifications.MatchNotificationDispatcher>.Instance),
                new ConfigurationBuilder().Build(), NullLogger<OfficialLineupCollector>.Instance).RunRoundAsync(now);
        }
        await Collect(World.Kickoff.AddMinutes(-45));
        using (var db = w.Db())
        {
            var req = db.PredictionRecomputeRequests.Single();
            Assert.Equal(("OfficialLineup", "Pending"), (req.TriggerType, req.Status));
            Assert.Equal(World.Kickoff.AddMinutes(-43), req.DueAtUtc);
        }

        // Debounce: 1 dk sonra işlenmez.
        Assert.Equal(0, (await w.Tick(World.Kickoff.AddMinutes(-44))).Claimed);

        // Aynı içerik ikinci kez gelirse ikinci istek yok.
        await Collect(World.Kickoff.AddMinutes(-40));
        using (var db = w.Db()) Assert.Equal(1, db.PredictionRecomputeRequests.Count());

        // Restart: önceki işçi kilidi alıp düştü → süresi dolan kilit yeni işçide devralınır.
        using (var db = w.Db())
        {
            var req = db.PredictionRecomputeRequests.Single();
            req.Status = "Processing"; req.LockedUntilUtc = World.Kickoff.AddMinutes(-39); req.Attempts = 1;
            db.SaveChanges();
        }
        var report = await w.Tick(World.Kickoff.AddMinutes(-38));
        Assert.Equal(1, report.Claimed);
        Assert.StartsWith("Published", report.Outcomes.Single().Outcome);

        using (var db = w.Db())
        {
            var req = db.PredictionRecomputeRequests.Single();
            Assert.Equal("Done", req.Status);
            var rows = db.MatchPredictionSnapshots.Where(s => s.MatchId == World.MatchId).OrderBy(s => s.ComputedAtUtc).ToList();
            Assert.Equal(2, rows.Count);                                   // eski snapshot silinmedi
            Assert.False(rows[0].IsCurrent);
            Assert.True(rows[1].IsCurrent);
            Assert.Equal(("OfficialLineup", first), (rows[1].TriggerType, rows[1].PreviousSnapshotId));
            var audit = JsonSerializer.Deserialize<OutcomeChangeAudit>(rows[1].ChangeAuditJson!)!;
            Assert.Contains("OfficialLineup:Verified", audit.NewInputs);
            Assert.Equal("Published", audit.Decision);
            // Doğrulanmış oyuncu etki modeli yok → kadro yüzdeyi değiştirmez.
            Assert.All(audit.Lines, l => Assert.Equal(0, l.Delta, 6));

            var reader = new MatchOutcomeSnapshotReader(db);
            var detail = await reader.GetCurrentAsync(World.MatchId);
            var discover = (await reader.GetCurrentForMatchesAsync(new[] { World.MatchId }))[World.MatchId];
            Assert.Equal(req.ResultSnapshotId, detail.SnapshotId);
            Assert.Equal(detail.SnapshotId, discover.SnapshotId);
            Assert.NotEqual(first, detail.SnapshotId);
            Assert.Equal(JsonSerializer.Serialize(detail), JsonSerializer.Serialize(discover));
        }

        // Debounce sürerken periyodik tur doğrulanmış olayı yakalarsa snapshot yine olaya atfedilir (canlı ölçüm: Lyon–Rennes saat değişikliği).
        using (var db = w.Db())
        {
            db.Matches.Single(m => m.Id == World.MatchId).MatchDate = World.Kickoff.AddHours(3);
            await PredictionRecomputeQueue.EnqueueAsync(db, World.MatchId, "KickoffChanged", "official:test-source", "critical:periodic-attribution", World.Kickoff.AddMinutes(-36));
            db.SaveChanges();
        }
        using (var db = w.Db()) Assert.Equal(1, (await w.Snapshots(db).RunAsync(World.Kickoff.AddMinutes(-35))).Written);
        using (var db = w.Db())
        {
            var latest = db.MatchPredictionSnapshots.Where(s => s.MatchId == World.MatchId && s.IsCurrent).Single();
            Assert.Equal(("KickoffChanged", "official:test-source"), (latest.TriggerType, latest.TriggerSource));
        }
        var queued = await w.Tick(World.Kickoff.AddMinutes(-33));
        Assert.StartsWith("Unchanged", queued.Outcomes.Single(o => o.MatchId == World.MatchId).Outcome);   // ikinci snapshot yok
        using (var db = w.Db()) Assert.Equal(3, db.MatchPredictionSnapshots.Count(s => s.MatchId == World.MatchId));

        // Geç yazılan gerçek sonuç (başlama saati eski) açıklanmış girdi sayılır: sonuç botu sonradan 7-2 yazar → NeedsReview değil.
        using (var db = w.Db())
        {
            var late = db.Matches.First(m => m.Status == MatchStatuses.Finished && (m.HomeTeamId == 1 || m.AwayTeamId == 1));
            db.Matches.Add(new Match { Id = 777001, LeagueId = 135, League = "Serie A", MatchDate = World.Kickoff.AddDays(-4), Status = MatchStatuses.Finished, HomeTeamId = 3, AwayTeamId = 1, HomeScore = 7, AwayScore = 2 });
            db.SaveChanges();
        }
        using (var db = w.Db())
        {
            var r = await w.Snapshots(db).RunAsync(World.Kickoff.AddMinutes(-31));
            Assert.Equal(0, r.NeedsReview);
            var latest = db.MatchPredictionSnapshots.AsNoTracking().Where(s => s.MatchId == World.MatchId).OrderByDescending(s => s.ComputedAtUtc).First();
            var audit = JsonSerializer.Deserialize<OutcomeChangeAudit>(latest.ChangeAuditJson!)!;
            Assert.True(audit.NewFinishedMatches >= 1);
        }

        // Kritik olmayan değişiklik yok → periyodik tur yeni snapshot yazmaz.
        using (var db = w.Db()) Assert.Equal(0, (await w.Snapshots(db).RunAsync(World.Kickoff.AddMinutes(-30))).Written);
    }

    [Fact]
    public async Task Yenileme_MacBasladiktanSonraTahminDegismez_Karne_KilitlenirVeDegerlendirilir()
    {
        var w = new World();
        using (var db = w.Db()) await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-3));
        using (var db = w.Db())
        {
            await PredictionRecomputeQueue.EnqueueAsync(db, World.MatchId, "KickoffChanged", "official:test", "critical:late", World.Kickoff.AddMinutes(1));
            db.SaveChanges();
        }
        var r = await w.Tick(World.Kickoff.AddMinutes(4));
        Assert.Equal("Skipped:KickoffPassed", r.Outcomes.Single().Outcome);
        Assert.Equal(1, r.ScorecardsLocked);
        using (var db = w.Db())
        {
            Assert.Equal(1, db.MatchPredictionSnapshots.Count(s => s.MatchId == World.MatchId));
            var card = db.PredictionScorecards.Single();
            Assert.Equal(db.MatchPredictionSnapshots.Single().SnapshotId, card.SnapshotId);
            Assert.True(card.PredictionCreatedAtUtc < card.KickoffUtc);
            var m = db.Matches.Single(x => x.Id == World.MatchId);
            m.Status = MatchStatuses.Finished; m.HomeScore = 2; m.AwayScore = 1;
            db.SaveChanges();
        }
        var settle = await w.Tick(World.Kickoff.AddHours(2));
        Assert.Equal(1, settle.ScorecardsSettled);
        using (var db = w.Db())
        {
            var card = db.PredictionScorecards.Single();
            Assert.Equal((2, 1), (card.FinalHomeScore!.Value, card.FinalAwayScore!.Value));
            Assert.NotNull(card.ResultCardCorrect);
            Assert.NotNull(card.ResultLogLoss);
        }
    }

    [Fact]
    public async Task LimitedLig_SnapshotYazilir_AmaKullaniciyaYuzdeGitmez_EskiModelSnapshotiDaYayimlanmaz()
    {
        var w = new World(PredictionEligibilities.Limited);
        using (var db = w.Db()) await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-3));
        using (var db = w.Db())
        {
            var row = db.MatchPredictionSnapshots.Single();
            Assert.Equal(PredictionEligibilities.Limited, row.PredictionEligibility);
            var user = await new MatchOutcomeSnapshotReader(db).GetCurrentAsync(World.MatchId);
            Assert.Equal("NotEligible", user.Status);
            Assert.Empty(user.MainCards);
            Assert.Empty(user.Families);

            // 2.0 snapshot'ı (uygunluk sınavı yok) kullanıcıya yüzde taşımaz.
            var dto = JsonSerializer.Deserialize<OutcomeSnapshotDto>(row.PayloadJson)!;
            dto.ModelVersion = OutcomeModelVersion.Previous; dto.PredictionEligibility = PredictionEligibilities.Enabled;
            row.PayloadJson = JsonSerializer.Serialize(dto);
            db.SaveChanges();
            Assert.Empty((await new MatchOutcomeSnapshotReader(db).GetCurrentAsync(World.MatchId)).MainCards);
        }
    }

    [Fact]
    public async Task ErtelemeDurumu_ResmiSonucYazicisi_YenilemeIstegiUretir_SnapshotDisabledOlur()
    {
        var w = new World();
        using (var db = w.Db()) await w.Snapshots(db).RunAsync(World.Kickoff.AddHours(-3));
        using (var db = w.Db())
        {
            var writer = new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance);
            var record = new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "sdp-vf", null, "Venezia", "Fiorentina", World.Kickoff, OfficialMatchStatuses.Postponed, null, null, "POSTPONED");
            var decision = OfficialResultStatusPolicy.Decide(record);
            var outcome = await writer.ApplyAsync(World.MatchId, new OfficialLineupTests.FakeSource(), record, decision, "r", World.Kickoff.AddHours(-2));
            Assert.Equal(OfficialResultWriter.StatusApplied, outcome.Outcome);
            Assert.Equal("StatusChange", db.PredictionRecomputeRequests.Single().TriggerType);
        }
        // Ertelenen maç artık "yaklaşan" değil ama güncel snapshot'ı Disabled'a çekilir.
        await w.Tick(World.Kickoff.AddHours(-1.9));
        using (var db = w.Db())
        {
            var current = db.MatchPredictionSnapshots.Single(s => s.IsCurrent);
            Assert.Equal(PredictionEligibilities.Disabled, current.PredictionEligibility);
            Assert.Contains("MATCH_NOT_SCHEDULED", current.EligibilityReasonsJson);
        }
    }

    // ═══ 6. AI ANALİZİ ↔ KART ÇELİŞKİ KAPISI ══════════════════════════════════

    private static OutcomeSnapshotDto EnabledSnap(double lh, double la)
    {
        var s = OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(lh, la), 140, new OutcomeModelParameters { UncertaintyMix = 0 }), "Venezia", "Fiorentina");
        s.PredictionEligibility = PredictionEligibilities.Enabled;
        return s;
    }

    private static MatchAnalysisDto Analysis(params string[] whyWatch) => new()
    {
        Status = "Ready", WhyWatch = whyWatch.ToList(),
        Scenarios = new List<MatchAnalysisScenarioDto> { new() { Market = "Beraberlik", Support = "x", Risk = "y" }, new() { Market = "Karşılıklı Gol Var", Support = "Venezia 3 kez gol buldu.", Risk = "r" } }
    };

    [Fact]
    public void Celiski_AltKartiIleYuksekSkorMetni_KGYokIleIkiTakimGolMetni_Engellenir()
    {
        var low = EnabledSnap(0.7, 0.5);                                   // düşük gol beklentisi → Alt ve KG Yok
        Assert.EndsWith("Alt", low.MainCards.Single(c => c.Family == OutcomeFamilies.Goals).Market);
        Assert.Equal(OddsMarketKeys.BttsNo, low.MainCards.Single(c => c.Family == OutcomeFamilies.Btts).MarketKey);
        var r = AnalysisConsistencyValidator.Validate(Analysis(
            "Gol ortalaması iki tarafta da yüksek: Venezia maçlarında 3,4, Fiorentina maçlarında 3,1.",
            "Venezia son 5 lig maçında 3 kez gol yemedi.",
            "Venezia ve Fiorentina maçlarında iki takım da gol buldu."), low, "Venezia", "Fiorentina");
        Assert.Contains(r.Violations, v => v.Kind == "GOALS_UNDER_VS_HIGH_SCORING_TEXT");
        Assert.Contains(r.Violations, v => v.Kind == "BTTS_NO_VS_BOTH_SCORE_TEXT");
        Assert.Single(r.Analysis.WhyWatch);
        Assert.DoesNotContain(r.Analysis.Scenarios, s => low.MainCards.All(c => c.Market != s.Market));   // yalnız gösterilen kartların senaryosu
    }

    [Fact]
    public void Celiski_SonucTarafi_LimitedKesinDil_GenelSablon_KartGerekcesiKodDisi_Engellenir()
    {
        var away = EnabledSnap(0.8, 2.2);
        Assert.Equal(OddsMarketKeys.Ms2, away.MainCards[0].MarketKey);
        var side = AnalysisConsistencyValidator.Validate(Analysis("Venezia bu maçın favorisi görünüyor."), away, "Venezia", "Fiorentina");
        Assert.Contains(side.Violations, v => v.Kind == "RESULT_SIDE_CONTRADICTION");

        var limited = EnabledSnap(1.5, 1.1);
        limited.PredictionEligibility = PredictionEligibilities.Limited;
        var lim = AnalysisConsistencyValidator.Validate(Analysis("Fiorentina net favori.", "Venezia son 5 maçında 2 galibiyet aldı."), OutcomeSnapshotBuilder.ForUser(limited), "Venezia", "Fiorentina");
        Assert.Contains(lim.Violations, v => v.Kind == "NOT_ELIGIBLE_CERTAIN_LANGUAGE");
        Assert.Empty(lim.Analysis.Scenarios);                              // Limited tahminde market senaryosu yok
        Assert.Single(lim.Analysis.WhyWatch);

        var generic = AnalysisConsistencyValidator.Validate(Analysis("Maç çevresinde konuşulacak gelişmeler var."), away, "Venezia", "Fiorentina");
        Assert.Contains(generic.Violations, v => v.Kind == "GENERIC_TEMPLATE");
        Assert.Equal("Unavailable", generic.Analysis.Status);

        var card = EnabledSnap(1.3, 1.25);
        card.MainCards[0].Reason = "Model beklenen golü hesaplıyor; ev sahibinin reyting üstünlüğü bu sonuca en yüksek payı veriyor.";
        card.MainCards[0].ReasonCodes = new List<string> { "RESULT_BALANCED" };
        Assert.Contains(AnalysisConsistencyValidator.ValidateCardReasons(card), v => v.Kind == "REASON_WITHOUT_CODE");
        Assert.Null(card.MainCards[0].Reason);

        // Kurucunun kendi gerekçesi kodla her zaman uyumlu.
        var rnd = new Random(9);
        for (var i = 0; i < 200; i++)
        {
            var s = EnabledSnap(0.4 + rnd.NextDouble() * 2.5, 0.4 + rnd.NextDouble() * 2.5);
            Assert.Empty(AnalysisConsistencyValidator.ValidateCardReasons(s));
        }
    }

    // ═══ 7. SONUÇ KAYNAKLARI VE KAPALI İŞLER ══════════════════════════════════

    [Fact]
    public void Uefa_GercekYanit_FinalSkorYayinAniKimlik_UzatmaPenaltiDurumlari()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "tests", "Formax.Tests", "Fixtures", "OfficialSources", "uefa-uel-2026-09-16.json"));
        var records = UefaMatchApiSource.Parse(json);
        var omonia = records.Single(r => r.OfficialMatchId == "2050063");
        Assert.Equal(("Omonia", "Celta", OfficialMatchStatuses.Finished, 1, 0), (omonia.HomeName, omonia.AwayName, omonia.Status, omonia.HomeScore!.Value, omonia.AwayScore!.Value));
        Assert.Equal(new DateTime(2026, 9, 16, 16, 45, 0, DateTimeKind.Utc), omonia.KickoffUtc);
        Assert.True(omonia.Extra!.ContainsKey("sourcePublishedAtUtc"));
        Assert.Equal("Real Club Celta", omonia.Extra["awayAltName"]);
        Assert.True(OfficialTeamNameMatcher.SameTeam(omonia.HomeName, "Omonia Nicosia"));
        var draw = records.Single(r => r.OfficialMatchId == "2050061");
        Assert.Equal((0, 0), (draw.HomeScore!.Value, draw.AwayScore!.Value));
        Assert.Equal("Final", OfficialResultStatusPolicy.Decide(omonia).Kind);

        // Kimlik: UEFA yazımı ile FORMAX kayıt adı farklı olan takımlar kısa/resmî ad ve takma adla eşleşir; yanlış takım eşleşmez.
        var beer = records.Single(r => r.OfficialMatchId == "2050064");
        Assert.True(OfficialMatchIdentityResolver.HomeMatches(beer, "Hapoel Beer Sheva"));
        Assert.True(OfficialMatchIdentityResolver.AwayMatches(beer, "Dinamo Zagreb"));
        var oly = records.Single(r => r.OfficialMatchId == "2050059");
        Assert.True(OfficialMatchIdentityResolver.HomeMatches(oly, "Olympiakos Piraeus"));
        Assert.False(OfficialMatchIdentityResolver.HomeMatches(oly, "Olympic Charleroi"));
        Assert.False(OfficialMatchIdentityResolver.AwayMatches(beer, "Dinamo Kiev"));

        // 17.09.2026 ölçülen gerçek eşleşme hataları: LALIGA "Real Racing Club SAD" ↔ "Racing Santander"; Ligue 1 officialName "Lyon".
        Assert.True(OfficialTeamNameMatcher.SameTeam("Real Racing Club SAD", "Racing Santander"));
        Assert.True(OfficialTeamNameMatcher.SameTeam("R. Racing Club", "Racing Santander"));
        Assert.False(OfficialTeamNameMatcher.SameTeam("Real Racing Club SAD", "Real Madrid"));
        Assert.False(OfficialTeamNameMatcher.SameTeam("Real Madrid", "Atletico Madrid"));
        var ligue1 = """
            {"matches":[{"matchId":"l1_championship_match_73854","date":"2026-09-12T18:45:00.000Z","period":"fullTime","isLive":false,"home":{"score":0,"clubIdentity":{"name":"Paris FC","officialName":"Paris FC","shortName":"Paris FC"}},"away":{"score":0,"clubIdentity":{"name":"Olympique Lyonnais","officialName":"Lyon","shortName":"OL"}}}]}
            """;
        var paris = Ligue1ApiSource.ParseMatches(ligue1).Single();
        Assert.True(OfficialMatchIdentityResolver.AwayMatches(paris, "Lyon"));
        Assert.True(OfficialMatchIdentityResolver.HomeMatches(paris, "Paris FC"));
        Assert.False(OfficialMatchIdentityResolver.AwayMatches(paris, "Paris Saint Germain"));

        Assert.Equal(OfficialMatchStatuses.FinishedAfterPenalties, UefaMatchApiSource.MapStatus("FINISHED", "WIN_ON_PENALTIES", true));
        Assert.Equal(OfficialMatchStatuses.FinishedAfterExtraTime, UefaMatchApiSource.MapStatus("FINISHED", "WIN_EXTRA_TIME", false));
        Assert.Equal(OfficialMatchStatuses.Postponed, UefaMatchApiSource.MapStatus("POSTPONED", null, false));
        Assert.Equal(OfficialMatchStatuses.Scheduled, UefaMatchApiSource.MapStatus("UPCOMING", null, false));
        // Robots yönlendirmesi yalnız aynı kayıtlı alan adında izlenir.
        Assert.Equal(OfficialContentFetcher.RegistrableDomain("match.uefa.com"), OfficialContentFetcher.RegistrableDomain("www.uefa.com"));
        Assert.NotEqual(OfficialContentFetcher.RegistrableDomain("match.uefa.com"), OfficialContentFetcher.RegistrableDomain("uefa.evil.com"));
    }

    [Fact]
    public async Task IstatistikBotu_VarsayilanKapali_MacSonrasiVeriAsamasiKapali()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var job = new OfficialStatisticsBotJob(services.GetRequiredService<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), NullLogger<OfficialStatisticsBotJob>.Instance);
        await job.StartAsync(CancellationToken.None);
        Assert.True(job.ExecuteTask!.IsCompleted);                          // yapılandırma yoksa hemen döner — hiç tur yok
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "Formax.Infrastructure", "BackgroundJobs", "PostMatchEnrichmentJob.cs"));
        Assert.Contains("GetValue(\"PostMatch:Data:Source\", \"Disabled\")", src);
    }

    [Fact]
    public void SayfaAcilisi_TahminUretmez_DisIstekYapmaz_OkuyucularSaltDb()
    {
        var reader = typeof(MatchOutcomeSnapshotReader).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Equal(new[] { typeof(FormaxDbContext) }, reader);
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "Formax.API", "Controllers", "MatchOutcomesController.cs"));
        Assert.DoesNotContain("SnapshotService", src);
        Assert.DoesNotContain("Fetcher", src);
        var validator = File.ReadAllText(Path.Combine(RepoRoot(), "Formax.Application", "Services", "Outcomes", "AnalysisConsistencyValidator.cs"));
        Assert.DoesNotContain("HttpClient", validator);
        Assert.DoesNotContain("ILLMClient", validator);
    }
}
