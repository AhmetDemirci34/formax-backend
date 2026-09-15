using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    public sealed class BinaryMetric
    {
        public int Count { get; set; }
        public double LogLoss { get; set; }
        public double Brier { get; set; }
        public double Accuracy { get; set; }
    }

    public sealed class ReliabilityBand
    {
        public string Band { get; set; } = string.Empty;
        public int Count { get; set; }
        public double MeanPredicted { get; set; }
        public double ObservedFrequency { get; set; }
    }

    public sealed class ModelVariantMetrics
    {
        public string Variant { get; set; } = string.Empty;
        public int Matches { get; set; }
        public double ResultLogLoss { get; set; }
        public double ResultBrier { get; set; }
        public double ResultAccuracy { get; set; }
        public BinaryMetric Over25 { get; set; } = new();
        public BinaryMetric Over15 { get; set; } = new();
        public BinaryMetric Over35 { get; set; } = new();
        public BinaryMetric Btts { get; set; } = new();
        /// <summary>Beklenen kalibrasyon hatası (10 dilim, bütün ikili olaylar havuzu).</summary>
        public double CalibrationError { get; set; }
        public double CombinedLogLoss { get; set; }
        public List<ReliabilityBand> Bands { get; set; } = new();
    }

    public sealed class LeagueMetric
    {
        public int LeagueId { get; set; }
        public int Matches { get; set; }
        public double ResultLogLossCalibrated { get; set; }
        public double ResultLogLossRaw { get; set; }
        public double ResultLogLossLeagueBaseline { get; set; }
        public double GoalScale { get; set; }
    }

    public sealed class MainCardAudit
    {
        public int Matches { get; set; }
        public int DoubleChanceCards { get; set; }
        public Dictionary<string, int> FamilyDistribution { get; set; } = new();
        public Dictionary<string, int> MarketDistribution { get; set; } = new();
        public int DistinctTriples { get; set; }
        public int MostRepeatedTripleCount { get; set; }
        public string? MostRepeatedTriple { get; set; }
        public List<ReliabilityBand> MainCardHitRateByBand { get; set; } = new();
        /// <summary>Ana kartlarda ≥ %70 yüzdelerin gerçekleşme oranı.</summary>
        public double HighProbabilityHitRate { get; set; }
        public int HighProbabilityCards { get; set; }
    }

    public sealed class LegacyRankingAudit
    {
        public int Matches { get; set; }
        /// <summary>Eski kural (ham yüzdeye göre ilk 3) — ilk kartın çifte şans olma oranı.</summary>
        public double TopCardDoubleChanceShare { get; set; }
        /// <summary>Eski kural — ilk 3 kartın içinde en az bir çifte şans bulunma oranı.</summary>
        public double AnyOfTop3DoubleChanceShare { get; set; }
        /// <summary>Eski kural — ilk 3 kartın toplamında çifte şans payı.</summary>
        public double Top3CardsDoubleChanceShare { get; set; }
    }

    public sealed class OutcomeBacktestReport
    {
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public DateTime EvalStartUtc { get; set; }
        public DateTime CalibrationStartUtc { get; set; }
        public DateTime TestStartUtc { get; set; }
        public DateTime TestEndUtc { get; set; }
        public int HistoricalMatchesProcessed { get; set; }
        public int TrainMatches { get; set; }
        public int CalibrationMatches { get; set; }
        public int TestMatches { get; set; }
        public int InsufficientDataMatches { get; set; }
        public Dictionary<string, double> LearningRateSearch { get; set; } = new();
        public double ChosenLearningRate { get; set; }
        public bool CalibrationApplied { get; set; }
        public double CalibrationWindowImprovement { get; set; }
        public ModelVariantMetrics TestRaw { get; set; } = new();
        public ModelVariantMetrics TestCalibrated { get; set; } = new();
        public ModelVariantMetrics TestLeagueBaseline { get; set; } = new();
        public List<LeagueMetric> Leagues { get; set; } = new();
        public MainCardAudit MainCards { get; set; } = new();
        public LegacyRankingAudit LegacyRanking { get; set; } = new();
        public OutcomeModelParameters Parameters { get; set; } = new();
        public string Decision { get; set; } = string.Empty;
    }

    /// <summary>
    /// ZAMANSAL GERİYE DÖNÜK TEST — maçlar başlama saatine göre sırayla işlenir: her maçın tahmini yalnız ondan ÖNCE bitmiş
    /// maçlardan kurulmuş reytingle yapılır, sonra maç modele eklenir (sızıntı yok). Pencereler:
    ///   eğitim [evalStart, calStart) → öğrenme oranı seçimi;
    ///   kalibrasyon [calStart, testStart) → kalibrasyon parametreleri seçimi;
    ///   test [testStart, testEnd) → YALNIZ raporlama (test penceresine bakılarak hiçbir parametre seçilmez).
    /// </summary>
    public static class OutcomeBacktest
    {
        public static readonly double[] LearningRates = { 0.03, 0.05, 0.07, 0.10 };
        private static readonly double[] GoalScales = { 0.92, 0.96, 1.0, 1.04, 1.08 };
        private static readonly double[] DrawInflations = { 0.95, 1.0, 1.08, 1.16, 1.25 };
        private static readonly double[] BaselineMixes = { 0.0, 0.05, 0.12, 0.2 };
        private static readonly double[] UncertaintyMixes = { 0.0, 0.2, 0.4, 0.6 };
        public const double LeagueShrinkageK = 200;
        public const double MinimumCalibrationGain = 0.0005;

        private readonly record struct Sample(int LeagueId, DateTime KickoffUtc, OutcomeExpectation E, int HomeGoals, int AwayGoals);

        public static OutcomeBacktestReport Run(IReadOnlyList<HistoricalMatch> ordered, ISet<int> evalLeagues,
            DateTime evalStart, DateTime calStart, DateTime testStart, DateTime testEnd)
        {
            var report = new OutcomeBacktestReport { EvalStartUtc = evalStart, CalibrationStartUtc = calStart, TestStartUtc = testStart, TestEndUtc = testEnd, HistoricalMatchesProcessed = ordered.Count };

            // 1) Öğrenme oranı — eğitim penceresinde ham (kalibrasyonsuz) birleşik log loss.
            var identity = new OutcomeModelParameters { GoalScale = 1, DrawInflation = 1, BaselineMix = 0, UncertaintyMix = 0 };
            double bestLoss = double.MaxValue, bestEta = LearningRates[1];
            foreach (var eta in LearningRates)
            {
                var p = identity.Clone(); p.LearningRate = eta;
                var samples = Collect(ordered, evalLeagues, p, evalStart, calStart, out _);
                var loss = samples.Count == 0 ? double.MaxValue : samples.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                report.LearningRateSearch[eta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)] = Math.Round(loss, 5);
                if (loss < bestLoss) { bestLoss = loss; bestEta = eta; report.TrainMatches = samples.Count; }
            }
            report.ChosenLearningRate = bestEta;

            // 2) Seçilen oranla kalibrasyon + test örnekleri (tek geçiş).
            var baseParams = identity.Clone(); baseParams.LearningRate = bestEta;
            var all = Collect(ordered, evalLeagues, baseParams, calStart, testEnd, out var insufficient);
            report.InsufficientDataMatches = insufficient;
            var cal = all.Where(s => s.KickoffUtc < testStart).ToList();
            var test = all.Where(s => s.KickoffUtc >= testStart).ToList();
            report.CalibrationMatches = cal.Count;
            report.TestMatches = test.Count;

            // 3) Kalibrasyon — yalnız kalibrasyon penceresi.
            var chosen = baseParams.Clone();
            if (cal.Count >= 200)
            {
                var identityLoss = cal.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Calibrated, s));
                double best = double.MaxValue;
                OutcomeModelParameters? bestP = null;
                foreach (var gs in GoalScales)
                foreach (var d in DrawInflations)
                foreach (var bm in BaselineMixes)
                foreach (var um in UncertaintyMixes)
                {
                    var p = baseParams.Clone(); p.GoalScale = gs; p.DrawInflation = d; p.BaselineMix = bm; p.UncertaintyMix = um;
                    var loss = cal.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                    if (loss < best) { best = loss; bestP = p; }
                }
                report.CalibrationWindowImprovement = Math.Round(identityLoss - best, 5);
                if (bestP != null && identityLoss - best >= MinimumCalibrationGain)
                {
                    chosen = bestP;
                    report.CalibrationApplied = true;
                    // Lig başına gol ölçeği — küçük örneklemde global değere daraltma.
                    foreach (var g in cal.GroupBy(s => s.LeagueId))
                    {
                        var n = g.Count();
                        double lb = double.MaxValue, lScale = chosen.GoalScale;
                        foreach (var gs in GoalScales)
                        {
                            var p = chosen.Clone(); p.LeagueGoalScale.Clear(); p.GoalScale = gs;
                            var loss = g.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                            if (loss < lb) { lb = loss; lScale = gs; }
                        }
                        chosen.LeagueGoalScale[g.Key] = Math.Round((n * lScale + LeagueShrinkageK * chosen.GoalScale) / (n + LeagueShrinkageK), 4);
                    }
                }
            }
            report.Parameters = chosen;

            // 4) Test — yalnız raporlama.
            report.TestRaw = Evaluate("Raw", test, s => OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Raw);
            report.TestCalibrated = Evaluate("Calibrated", test, s => OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated);
            report.TestLeagueBaseline = Evaluate("LeagueAverageBaseline", test, s => OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Baseline);
            report.Leagues = test.GroupBy(s => s.LeagueId).Select(g => new LeagueMetric
            {
                LeagueId = g.Key,
                Matches = g.Count(),
                ResultLogLossCalibrated = Math.Round(g.Average(s => ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated, s)), 4),
                ResultLogLossRaw = Math.Round(g.Average(s => ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Raw, s)), 4),
                ResultLogLossLeagueBaseline = Math.Round(g.Average(s => ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Baseline, s)), 4),
                GoalScale = chosen.LeagueGoalScale.TryGetValue(g.Key, out var sc) ? sc : chosen.GoalScale
            }).OrderBy(l => l.LeagueId).ToList();
            report.MainCards = AuditMainCards(test, chosen);
            report.LegacyRanking = AuditLegacy(test, baseParams);
            report.Decision = report.CalibrationApplied
                ? (report.TestCalibrated.CombinedLogLoss <= report.TestRaw.CombinedLogLoss ? "CALIBRATION_APPLIED_TEST_IMPROVED" : "CALIBRATION_APPLIED_TEST_NOT_IMPROVED")
                : "RAW_MODEL_KEPT_NO_CALIBRATION_GAIN";
            return report;
        }

        private static List<Sample> Collect(IReadOnlyList<HistoricalMatch> ordered, ISet<int> evalLeagues, OutcomeModelParameters p,
            DateTime from, DateTime to, out int insufficient)
        {
            var model = new OutcomeRatingModel(p);
            var list = new List<Sample>();
            insufficient = 0;
            foreach (var m in ordered)
            {
                if (m.KickoffUtc >= to) break;
                if (m.KickoffUtc >= from && evalLeagues.Contains(m.LeagueId))
                {
                    var e = model.Expect(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);
                    if (e.Sufficient) list.Add(new Sample(m.LeagueId, m.KickoffUtc, e, m.HomeGoals, m.AwayGoals));
                    else insufficient++;
                }
                model.Update(m);
            }
            return list;
        }

        private const double Eps = 1e-6;

        private static double ResultLoss(ScoreDistribution d, Sample s)
        {
            var p = s.HomeGoals > s.AwayGoals ? d.HomeWin : s.HomeGoals == s.AwayGoals ? d.Draw : d.AwayWin;
            return -Math.Log(Math.Max(Eps, p));
        }

        private static double BinLoss(double p, bool y) => -Math.Log(Math.Max(Eps, y ? p : 1 - p));

        private static double Combined(ScoreDistribution d, Sample s)
            => ResultLoss(d, s) + BinLoss(d.Over(2.5), s.HomeGoals + s.AwayGoals > 2.5) + BinLoss(d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0);

        private static ModelVariantMetrics Evaluate(string name, List<Sample> samples, Func<Sample, ScoreDistribution> dist)
        {
            var m = new ModelVariantMetrics { Variant = name, Matches = samples.Count };
            if (samples.Count == 0) return m;
            double rl = 0, rb = 0, ra = 0;
            var pooled = new List<(double P, bool Y)>();
            var o15 = new List<(double, bool)>(); var o25 = new List<(double, bool)>(); var o35 = new List<(double, bool)>(); var bt = new List<(double, bool)>();
            foreach (var s in samples)
            {
                var d = dist(s);
                var hw = s.HomeGoals > s.AwayGoals; var dr = s.HomeGoals == s.AwayGoals; var aw = s.HomeGoals < s.AwayGoals;
                rl += ResultLoss(d, s);
                rb += Sq(d.HomeWin - (hw ? 1 : 0)) + Sq(d.Draw - (dr ? 1 : 0)) + Sq(d.AwayWin - (aw ? 1 : 0));
                var arg = new[] { d.HomeWin, d.Draw, d.AwayWin };
                var pick = Array.IndexOf(arg, arg.Max());
                if ((pick == 0 && hw) || (pick == 1 && dr) || (pick == 2 && aw)) ra++;
                var total = s.HomeGoals + s.AwayGoals;
                o15.Add((d.Over(1.5), total > 1)); o25.Add((d.Over(2.5), total > 2)); o35.Add((d.Over(3.5), total > 3));
                bt.Add((d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0));
                pooled.Add((d.HomeWin, hw)); pooled.Add((d.Draw, dr)); pooled.Add((d.AwayWin, aw));
                pooled.Add((d.Over(1.5), total > 1)); pooled.Add((d.Under(1.5), total <= 1));
                pooled.Add((d.Over(2.5), total > 2)); pooled.Add((d.Under(2.5), total <= 2));
                pooled.Add((d.Over(3.5), total > 3)); pooled.Add((d.Under(3.5), total <= 3));
                pooled.Add((d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0)); pooled.Add((d.BttsNo, !(s.HomeGoals > 0 && s.AwayGoals > 0)));
            }
            var n = samples.Count;
            m.ResultLogLoss = Math.Round(rl / n, 5);
            m.ResultBrier = Math.Round(rb / n, 5);
            m.ResultAccuracy = Math.Round(ra / n, 4);
            m.Over15 = Bin(o15); m.Over25 = Bin(o25); m.Over35 = Bin(o35); m.Btts = Bin(bt);
            m.CombinedLogLoss = Math.Round(m.ResultLogLoss + m.Over25.LogLoss + m.Btts.LogLoss, 5);
            m.CalibrationError = Math.Round(Ece(pooled, 10), 5);
            m.Bands = Bands(pooled);
            return m;
        }

        private static double Sq(double v) => v * v;

        private static BinaryMetric Bin(List<(double P, bool Y)> xs) => new()
        {
            Count = xs.Count,
            LogLoss = Math.Round(xs.Average(x => BinLoss(x.P, x.Y)), 5),
            Brier = Math.Round(xs.Average(x => Sq(x.P - (x.Y ? 1 : 0))), 5),
            Accuracy = Math.Round(xs.Average(x => (x.P >= 0.5) == x.Y ? 1.0 : 0.0), 4)
        };

        public static double Ece(IReadOnlyList<(double P, bool Y)> xs, int bins)
        {
            if (xs.Count == 0) return 0;
            double ece = 0;
            foreach (var g in xs.GroupBy(x => Math.Min(bins - 1, (int)(x.P * bins))))
                ece += g.Count() / (double)xs.Count * Math.Abs(g.Average(x => x.P) - g.Average(x => x.Y ? 1.0 : 0.0));
            return ece;
        }

        public static List<ReliabilityBand> Bands(IReadOnlyList<(double P, bool Y)> xs)
        {
            var defs = new (string Name, double Lo, double Hi)[] { ("40-49", 0.40, 0.50), ("50-59", 0.50, 0.60), ("60-69", 0.60, 0.70), ("70-79", 0.70, 0.80), ("80+", 0.80, 1.01) };
            return defs.Select(b =>
            {
                var g = xs.Where(x => x.P >= b.Lo && x.P < b.Hi).ToList();
                return new ReliabilityBand
                {
                    Band = b.Name, Count = g.Count,
                    MeanPredicted = g.Count == 0 ? 0 : Math.Round(g.Average(x => x.P), 4),
                    ObservedFrequency = g.Count == 0 ? 0 : Math.Round(g.Average(x => x.Y ? 1.0 : 0.0), 4)
                };
            }).ToList();
        }

        private static MainCardAudit AuditMainCards(List<Sample> test, OutcomeModelParameters p)
        {
            var audit = new MainCardAudit { Matches = test.Count };
            var triples = new Dictionary<string, int>();
            var hits = new List<(double P, bool Y)>();
            foreach (var s in test)
            {
                var snap = OutcomeSnapshotBuilder.Build(0, OutcomePredictor.Predict(s.E, s.LeagueId, p), "Ev", "Deplasman");
                var key = string.Join(" | ", snap.MainCards.Select(c => c.Market));
                triples[key] = triples.GetValueOrDefault(key) + 1;
                foreach (var c in snap.MainCards)
                {
                    if (c.MarketKey != null && OutcomeFamilies.IsCompound(c.MarketKey)) audit.DoubleChanceCards++;
                    audit.FamilyDistribution[c.Family] = audit.FamilyDistribution.GetValueOrDefault(c.Family) + 1;
                    audit.MarketDistribution[c.Market] = audit.MarketDistribution.GetValueOrDefault(c.Market) + 1;
                    hits.Add((c.CalibratedProbability, Hit(c.MarketKey, s.HomeGoals, s.AwayGoals)));
                }
            }
            audit.DistinctTriples = triples.Count;
            if (triples.Count > 0)
            {
                var top = triples.OrderByDescending(t => t.Value).First();
                audit.MostRepeatedTriple = top.Key;
                audit.MostRepeatedTripleCount = top.Value;
            }
            audit.MainCardHitRateByBand = Bands(hits);
            var high = hits.Where(h => h.P >= 0.70).ToList();
            audit.HighProbabilityCards = high.Count;
            audit.HighProbabilityHitRate = high.Count == 0 ? 0 : Math.Round(high.Average(h => h.Y ? 1.0 : 0.0), 4);
            return audit;
        }

        /// <summary>
        /// ESKİ SEÇİM KURALININ AYNI TEST MAÇLARINDA YENİDEN OYNATILMASI — eski ekranlar olasılıkları ham yüzdeye göre sıralayıp
        /// ilk 3'ü gösteriyordu; eski market listesi (1X2, favori yönündeki TEK çifte şans, 2.5 alt/üst, KG var/yok, ilk yarı sonucu,
        /// ilk gol, en olası toplam gol bandı, en olası skor) aynı dağılımdan kurulup aynı kuralla sıralanır.
        /// </summary>
        private static LegacyRankingAudit AuditLegacy(List<Sample> test, OutcomeModelParameters p)
        {
            var audit = new LegacyRankingAudit { Matches = test.Count };
            if (test.Count == 0) return audit;
            int top1 = 0, any = 0, cards = 0;
            foreach (var s in test)
            {
                var d = OutcomePredictor.Predict(s.E, s.LeagueId, p).Raw;
                var half = ScoreDistribution.Poisson(d.ExpectedHome * 0.42, d.ExpectedAway * 0.42);
                var favHome = d.ExpectedHome >= d.ExpectedAway;
                var list = new List<(string M, double P, bool Dc)>
                {
                    ("MS1", d.HomeWin, false), ("MSX", d.Draw, false), ("MS2", d.AwayWin, false),
                    (favHome ? "1X" : "X2", favHome ? d.HomeWin + d.Draw : d.AwayWin + d.Draw, true),
                    ("O25", d.Over(2.5), false), ("U25", d.Under(2.5), false), ("KGV", d.BttsYes, false), ("KGY", d.BttsNo, false),
                    ("HT1", half.HomeWin, false), ("HTX", half.Draw, false), ("HT2", half.AwayWin, false),
                    ("FG_H", d.ExpectedHome / Math.Max(0.01, d.ExpectedTotalGoals) * (1 - Math.Exp(-d.ExpectedTotalGoals)), false),
                    ("FG_A", d.ExpectedAway / Math.Max(0.01, d.ExpectedTotalGoals) * (1 - Math.Exp(-d.ExpectedTotalGoals)), false),
                    ("BAND", new[] { d.TotalBetween(0, 1), d.TotalBetween(2, 3), d.TotalBetween(4, 99) }.Max(), false),
                    ("SCORE", d.TopScores(1)[0].P, false)
                };
                var top3 = list.OrderByDescending(x => x.P).ThenBy(x => x.M, StringComparer.Ordinal).Take(3).ToList();
                if (top3[0].Dc) top1++;
                if (top3.Any(x => x.Dc)) any++;
                cards += top3.Count(x => x.Dc);
            }
            audit.TopCardDoubleChanceShare = Math.Round(top1 / (double)test.Count, 4);
            audit.AnyOfTop3DoubleChanceShare = Math.Round(any / (double)test.Count, 4);
            audit.Top3CardsDoubleChanceShare = Math.Round(cards / (3.0 * test.Count), 4);
            return audit;
        }

        public static bool Hit(string? key, int h, int a) => key switch
        {
            Domain.Constants.OddsMarketKeys.Ms1 => h > a,
            Domain.Constants.OddsMarketKeys.MsX => h == a,
            Domain.Constants.OddsMarketKeys.Ms2 => h < a,
            Domain.Constants.OddsMarketKeys.Over15 => h + a > 1,
            Domain.Constants.OddsMarketKeys.Under15 => h + a < 2,
            Domain.Constants.OddsMarketKeys.Over25 => h + a > 2,
            Domain.Constants.OddsMarketKeys.Under25 => h + a < 3,
            Domain.Constants.OddsMarketKeys.Over35 => h + a > 3,
            Domain.Constants.OddsMarketKeys.Under35 => h + a < 4,
            Domain.Constants.OddsMarketKeys.BttsYes => h > 0 && a > 0,
            Domain.Constants.OddsMarketKeys.BttsNo => h == 0 || a == 0,
            _ => false
        };
    }
}
