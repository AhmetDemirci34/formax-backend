using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>Tahmin uygunluk durumları — kullanıcıya yüzde YALNIZ Enabled'da gösterilir.</summary>
    public static class PredictionEligibilities
    {
        public const string Enabled = "Enabled";
        public const string Limited = "Limited";
        public const string Disabled = "Disabled";
    }

    /// <summary>Zamansal test penceresindeki tek değerlendirme örneği (model + lig ortalaması tabanı aynı maçta).</summary>
    public sealed record EvalSample(
        int MatchId, int LeagueId, DateTime KickoffUtc, OutcomeExpectation E, int HomeGoals, int AwayGoals,
        double BaseHome, double BaseDraw, double BaseAway, double BaseOver25, double BaseBtts);

    /// <summary>Bir grubun (lig ya da ligler arası maç kümesi) test metrikleri.</summary>
    public sealed class GroupMetrics
    {
        public string Group { get; set; } = string.Empty;
        public int? LeagueId { get; set; }
        public int Matches { get; set; }
        /// <summary>Test penceresinde tahmin üretilemeyen (yetersiz veri / kapı) maç sayısı.</summary>
        public int NotPredicted { get; set; }
        public double DataCoverage { get; set; }
        public double ResultLogLoss { get; set; }
        public double BaselineResultLogLoss { get; set; }
        public double ResultBrier { get; set; }
        public double BaselineResultBrier { get; set; }
        public double ResultAccuracy { get; set; }
        public double CalibrationError { get; set; }
        /// <summary>Ortalama tahmin − gerçekleşme (ev sahibi galibiyeti).</summary>
        public double HomeBias { get; set; }
        public double DrawBias { get; set; }
        public double AwayBias { get; set; }
        public double ActualHomeRate { get; set; }
        public double ActualDrawRate { get; set; }
        public double MeanPredictedHome { get; set; }
        public double MeanPredictedDraw { get; set; }
        public double Over25LogLoss { get; set; }
        public double BaselineOver25LogLoss { get; set; }
        public double BttsLogLoss { get; set; }
        public double BaselineBttsLogLoss { get; set; }
        /// <summary>Maç başına (model − taban) 1X2 log loss farkı; negatif = model iyi.</summary>
        public double LogLossDiff { get; set; }
        public double LogLossDiffCiLow { get; set; }
        public double LogLossDiffCiHigh { get; set; }
        public bool BetterThanBaseline { get; set; }
        public bool SignificantlyBetter { get; set; }
        public int FinishedLast60Days { get; set; }
        public string Eligibility { get; set; } = PredictionEligibilities.Disabled;
        public List<string> EligibilityReasons { get; set; } = new();
    }

    /// <summary>
    /// UYGUNLUK POLİTİKASI (eligibility-1) — her lig bağımsız sınavdan geçer. Eşikler burada, sürümlü ve testlidir.
    /// Enabled: ≥ 300 zamansal test maçı; 1X2 log loss farkının %95 bootstrap güven aralığının üst ucu &lt; 0 (taban lig ortalamasından
    /// anlamlı iyi); ECE ≤ 0,03; |ev sahibi| ve |beraberlik| sapması ≤ 0,03; 2.5 Alt/Üst ve KG tabandan kötü değil (≤ +0,005);
    /// son 60 günde en az 5 bitmiş maç (güncel veri).
    /// Disabled: &lt; 100 test maçı, model tabandan KÖTÜ (ortalama fark &gt; 0), ya da ECE &gt; 0,06.
    /// Limited: arası (veri var ama örneklem ya da üstünlük kesin değil).
    /// </summary>
    public static class EligibilityPolicy
    {
        public const string Version = "eligibility-1";
        public const int EnabledMinMatches = 300;
        public const int DisabledBelowMatches = 100;
        public const double MaxCalibrationErrorEnabled = 0.03;
        public const double MaxCalibrationErrorLimited = 0.06;
        public const double MaxHomeDrawBias = 0.03;
        public const double GoalMarketTolerance = 0.005;
        public const int MinRecentFinished = 5;
        public const int BootstrapSamples = 2000;

        public static void Decide(GroupMetrics g)
        {
            var reasons = new List<string>();
            if (g.Matches < DisabledBelowMatches) reasons.Add("DISABLED_TOO_FEW_TEST_MATCHES");
            if (g.Matches > 0 && g.LogLossDiff > 0) reasons.Add("DISABLED_WORSE_THAN_BASELINE");
            if (g.CalibrationError > MaxCalibrationErrorLimited) reasons.Add("DISABLED_CALIBRATION_BROKEN");
            if (reasons.Count > 0)
            {
                g.Eligibility = PredictionEligibilities.Disabled;
                g.EligibilityReasons = reasons;
                return;
            }
            if (g.Matches < EnabledMinMatches) reasons.Add("LIMITED_SAMPLE_BELOW_300");
            if (!g.SignificantlyBetter) reasons.Add("LIMITED_NOT_SIGNIFICANTLY_BETTER");
            if (g.CalibrationError > MaxCalibrationErrorEnabled) reasons.Add("LIMITED_CALIBRATION_ERROR");
            if (Math.Abs(g.HomeBias) > MaxHomeDrawBias) reasons.Add("LIMITED_HOME_BIAS");
            if (Math.Abs(g.DrawBias) > MaxHomeDrawBias) reasons.Add("LIMITED_DRAW_BIAS");
            if (g.Over25LogLoss > g.BaselineOver25LogLoss + GoalMarketTolerance) reasons.Add("LIMITED_GOALS_WORSE_THAN_BASELINE");
            if (g.BttsLogLoss > g.BaselineBttsLogLoss + GoalMarketTolerance) reasons.Add("LIMITED_BTTS_WORSE_THAN_BASELINE");
            if (g.FinishedLast60Days < MinRecentFinished) reasons.Add("LIMITED_STALE_LEAGUE_DATA");
            g.Eligibility = reasons.Count == 0 ? PredictionEligibilities.Enabled : PredictionEligibilities.Limited;
            g.EligibilityReasons = reasons;
        }
    }

    public static class GroupEvaluator
    {
        private const double Eps = 1e-6;

        public static double ResultLoss(double pH, double pD, double pA, int hg, int ag)
            => -Math.Log(Math.Max(Eps, hg > ag ? pH : hg == ag ? pD : pA));

        public static double BinLoss(double p, bool y) => -Math.Log(Math.Max(Eps, y ? p : 1 - p));

        /// <summary>Grubun metrikleri. <paramref name="dist"/> örneğin kalibre dağılımını döner.</summary>
        public static GroupMetrics Evaluate(string group, int? leagueId, IReadOnlyList<EvalSample> samples, Func<EvalSample, ScoreDistribution> dist,
            int notPredicted, int seed = 17)
        {
            var g = new GroupMetrics { Group = group, LeagueId = leagueId, Matches = samples.Count, NotPredicted = notPredicted };
            var total = samples.Count + notPredicted;
            g.DataCoverage = total == 0 ? 0 : Math.Round(samples.Count / (double)total, 4);
            if (samples.Count == 0) return g;

            var diffs = new double[samples.Count];
            double rl = 0, bl = 0, rb = 0, bb = 0, acc = 0, ph = 0, pd = 0, pa = 0, ah = 0, ad = 0, aa = 0, o = 0, bo = 0, bt = 0, bbt = 0;
            var pooled = new List<(double, bool)>(samples.Count * 3);
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var d = dist(s);
                var hw = s.HomeGoals > s.AwayGoals; var dr = s.HomeGoals == s.AwayGoals; var aw = s.HomeGoals < s.AwayGoals;
                double h = d.HomeWin, x = d.Draw, a = d.AwayWin;
                var l = ResultLoss(h, x, a, s.HomeGoals, s.AwayGoals);
                var lb = ResultLoss(s.BaseHome, s.BaseDraw, s.BaseAway, s.HomeGoals, s.AwayGoals);
                rl += l; bl += lb; diffs[i] = l - lb;
                rb += Sq(h - (hw ? 1 : 0)) + Sq(x - (dr ? 1 : 0)) + Sq(a - (aw ? 1 : 0));
                bb += Sq(s.BaseHome - (hw ? 1 : 0)) + Sq(s.BaseDraw - (dr ? 1 : 0)) + Sq(s.BaseAway - (aw ? 1 : 0));
                var pick = h >= x && h >= a ? 0 : x >= a ? 1 : 2;
                if ((pick == 0 && hw) || (pick == 1 && dr) || (pick == 2 && aw)) acc++;
                ph += h; pd += x; pa += a; ah += hw ? 1 : 0; ad += dr ? 1 : 0; aa += aw ? 1 : 0;
                var over = s.HomeGoals + s.AwayGoals > 2; var btts = s.HomeGoals > 0 && s.AwayGoals > 0;
                o += BinLoss(d.Over(2.5), over); bo += BinLoss(s.BaseOver25, over);
                bt += BinLoss(d.BttsYes, btts); bbt += BinLoss(s.BaseBtts, btts);
                pooled.Add((h, hw)); pooled.Add((x, dr)); pooled.Add((a, aw));
            }
            var n = (double)samples.Count;
            g.ResultLogLoss = R(rl / n); g.BaselineResultLogLoss = R(bl / n);
            g.ResultBrier = R(rb / n); g.BaselineResultBrier = R(bb / n);
            g.ResultAccuracy = R(acc / n);
            g.CalibrationError = R(OutcomeBacktest.Ece(pooled, 10));
            g.MeanPredictedHome = R(ph / n); g.MeanPredictedDraw = R(pd / n);
            g.ActualHomeRate = R(ah / n); g.ActualDrawRate = R(ad / n);
            g.HomeBias = R((ph - ah) / n); g.DrawBias = R((pd - ad) / n); g.AwayBias = R((pa - aa) / n);
            g.Over25LogLoss = R(o / n); g.BaselineOver25LogLoss = R(bo / n);
            g.BttsLogLoss = R(bt / n); g.BaselineBttsLogLoss = R(bbt / n);
            g.LogLossDiff = R(diffs.Average());
            var (lo, hi) = BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, seed);
            g.LogLossDiffCiLow = R(lo); g.LogLossDiffCiHigh = R(hi);
            g.BetterThanBaseline = g.LogLossDiff < 0;
            g.SignificantlyBetter = hi < 0;
            return g;
        }

        /// <summary>Ortalamanın yüzdelik bootstrap %95 güven aralığı (deterministik tohum).</summary>
        public static (double Low, double High) BootstrapMeanCi(IReadOnlyList<double> xs, int resamples, int seed)
        {
            if (xs.Count == 0) return (0, 0);
            var rng = new Random(seed);
            var means = new double[resamples];
            for (var r = 0; r < resamples; r++)
            {
                double s = 0;
                for (var i = 0; i < xs.Count; i++) s += xs[rng.Next(xs.Count)];
                means[r] = s / xs.Count;
            }
            Array.Sort(means);
            return (means[(int)(0.025 * (resamples - 1))], means[(int)(0.975 * (resamples - 1))]);
        }

        private static double Sq(double v) => v * v;
        private static double R(double v) => Math.Round(v, 5);
    }
}
