using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>
    /// TAHMİN DOĞRULUĞU LABORATUVARI (26.09.2026) — üretim modelini DEĞİŞTİRMEZ. Aynı zamansal test örnekleri üzerinde taban
    /// modelleri, ablasyonları ve sınırlı sayıda aday iyileştirmeyi eşli ölçer.
    ///
    /// Kurallar: seçim YALNIZ validation (kalibrasyon penceresi) örnekleriyle yapılır; final test penceresine seçilmiş TEK aday
    /// bir kez uygulanır. Bütün özellikler maç başlamadan önce bilinen veriden gelir (aynı başlama saatindeki maçlar birbirini
    /// göremez). Bahis oranı girdi değildir. Rastgelelik yalnız sabit tohumlu bootstrap'tadır.
    /// </summary>
    public static class OutcomeAccuracyLab
    {
        /// <summary>Bağımsız ölçülen aileler (çifte şans 1X2'den türetilir, ayrı ölçülmez).</summary>
        public static readonly string[] Families =
        {
            MarketFamilies.MatchResult, MarketFamilies.TotalGoals15, MarketFamilies.TotalGoals25, MarketFamilies.TotalGoals35, MarketFamilies.BothTeamsToScore
        };

        /// <summary>Bir maçın bütün market olasılıkları. Bir model bir aileyi tahmin etmiyorsa değer NaN'dır.</summary>
        public sealed record Pred(double H, double D, double A, double O15, double O25, double O35, double Btts)
        {
            public static Pred From(ScoreDistribution d) => new(d.HomeWin, d.Draw, d.AwayWin, d.Over(1.5), d.Over(2.5), d.Over(3.5), d.BttsYes);

            public static Pred FromBaseline(EvalSample s) => new(s.BaseHome, s.BaseDraw, s.BaseAway, s.BaseOver15, s.BaseOver25, s.BaseOver35, s.BaseBtts);

            /// <summary>Yalnız 1X2 karışımı (gol marketleri bu modelinkidir).</summary>
            public Pred Mix1X2(Pred other, double w) => this with
            {
                H = (1 - w) * H + w * other.H, D = (1 - w) * D + w * other.D, A = (1 - w) * A + w * other.A
            };

            /// <summary>Çifte şans — 1X2'den matematiksel türetme.</summary>
            public (double X1, double X2, double H12) DoubleChance => (H + D, D + A, H + A);

            public double Binary(string family) => family switch
            {
                MarketFamilies.TotalGoals15 => O15,
                MarketFamilies.TotalGoals25 => O25,
                MarketFamilies.TotalGoals35 => O35,
                MarketFamilies.BothTeamsToScore => Btts,
                _ => throw new ArgumentOutOfRangeException(nameof(family))
            };
        }

        public static bool Outcome(string family, EvalSample s) => family switch
        {
            MarketFamilies.TotalGoals15 => s.HomeGoals + s.AwayGoals > 1,
            MarketFamilies.TotalGoals25 => s.HomeGoals + s.AwayGoals > 2,
            MarketFamilies.TotalGoals35 => s.HomeGoals + s.AwayGoals > 3,
            MarketFamilies.BothTeamsToScore => s.HomeGoals > 0 && s.AwayGoals > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

        private const double Eps = 1e-6;

        /// <summary>Maç başına log loss (1X2: çok sınıflı; ikili: Bernoulli).</summary>
        public static double Loss(string family, Pred p, EvalSample s)
        {
            if (family == MarketFamilies.MatchResult)
                return -Math.Log(Math.Max(Eps, s.HomeGoals > s.AwayGoals ? p.H : s.HomeGoals == s.AwayGoals ? p.D : p.A));
            var q = Math.Clamp(p.Binary(family), Eps, 1 - Eps);
            return Outcome(family, s) ? -Math.Log(q) : -Math.Log(1 - q);
        }

        public static double Brier(string family, Pred p, EvalSample s)
        {
            if (family == MarketFamilies.MatchResult)
            {
                double h = s.HomeGoals > s.AwayGoals ? 1 : 0, d = s.HomeGoals == s.AwayGoals ? 1 : 0, a = s.HomeGoals < s.AwayGoals ? 1 : 0;
                return Sq(p.H - h) + Sq(p.D - d) + Sq(p.A - a);
            }
            return Sq(p.Binary(family) - (Outcome(family, s) ? 1 : 0));
        }

        public sealed class Score
        {
            public int N { get; set; }
            public int NotPredicted { get; set; }
            public double Coverage { get; set; }
            public double LogLoss { get; set; }
            public double Brier { get; set; }
            public double Ece { get; set; }
            /// <summary>Yardımcı metrik — başarı kanıtı sayılmaz.</summary>
            public double Accuracy { get; set; }
        }

        /// <summary>Aile metrikleri. <paramref name="f"/> null dönerse maç tahmin edilmemiş sayılır (kapsam düşer).</summary>
        public static Score Evaluate(string family, IReadOnlyList<EvalSample> samples, Func<EvalSample, Pred?> f, int notPredictedUpstream = 0)
        {
            var pooled = new List<(double P, bool Y)>();
            double ll = 0, br = 0, acc = 0; var n = 0; var miss = notPredictedUpstream;
            foreach (var s in samples)
            {
                var p = f(s);
                if (p == null || (family == MarketFamilies.MatchResult ? double.IsNaN(p.H) : double.IsNaN(p.Binary(family)))) { miss++; continue; }
                n++;
                ll += Loss(family, p, s); br += Brier(family, p, s);
                if (family == MarketFamilies.MatchResult)
                {
                    bool hw = s.HomeGoals > s.AwayGoals, dr = s.HomeGoals == s.AwayGoals, aw = s.HomeGoals < s.AwayGoals;
                    pooled.Add((p.H, hw)); pooled.Add((p.D, dr)); pooled.Add((p.A, aw));
                    var top = Math.Max(p.H, Math.Max(p.D, p.A));
                    acc += (top == p.H ? hw : top == p.A ? aw : dr) ? 1 : 0;
                }
                else
                {
                    var q = p.Binary(family); var y = Outcome(family, s);
                    pooled.Add((q, y)); pooled.Add((1 - q, !y));
                    acc += (q >= 0.5) == y ? 1 : 0;
                }
            }
            return new Score
            {
                N = n, NotPredicted = miss, Coverage = n + miss == 0 ? 0 : R((double)n / (n + miss)),
                LogLoss = n == 0 ? double.NaN : R(ll / n), Brier = n == 0 ? double.NaN : R(br / n),
                Ece = n == 0 ? double.NaN : R(OutcomeBacktest.Ece(pooled, 10)), Accuracy = n == 0 ? double.NaN : R(acc / n)
            };
        }

        /// <summary>
        /// EŞLİ BLOK BOOTSTRAP — aynı maçlarda (aday − üretim) kayıp farkı; bloklar ISO haftasıdır (aynı haftanın maçları
        /// birlikte örneklenir; hafta içi bağımlılık aralığı daraltmasın). Sabit tohum → deterministik.
        /// </summary>
        public static (double Mean, double Low, double High) PairedBlockBootstrap(IReadOnlyList<(DateTime KickoffUtc, double Diff)> diffs, int samples = 2000, int seed = 2609)
        {
            if (diffs.Count == 0) return (0, 0, 0);
            var blocks = diffs.GroupBy(d => ISOWeek.GetYear(d.KickoffUtc) * 100 + ISOWeek.GetWeekOfYear(d.KickoffUtc))
                .OrderBy(g => g.Key).Select(g => (Sum: g.Sum(x => x.Diff), Count: g.Count())).ToArray();
            var mean = diffs.Average(d => d.Diff);
            var rng = new Random(seed);
            var means = new double[samples];
            for (var b = 0; b < samples; b++)
            {
                double s = 0; long c = 0;
                for (var i = 0; i < blocks.Length; i++) { var k = blocks[rng.Next(blocks.Length)]; s += k.Sum; c += k.Count; }
                means[b] = c == 0 ? 0 : s / c;
            }
            Array.Sort(means);
            return (R(mean), R(means[(int)(0.025 * (samples - 1))]), R(means[(int)(0.975 * (samples - 1))]));
        }

        // ═══════════════════════ BAĞIMSIZ DİNAMİK ELO (taban + ensemble adayı) ═══════════════════════

        /// <summary>
        /// Maç başına Elo logiti x = (R_ev − R_dep + EV_lig) · ln10 / 400 — maç başlamadan önceki durumdan. Aynı başlama saatindeki
        /// maçlar önce birlikte tahmin edilir, sonra birlikte güncellenir (eşzamanlı sonuç sızmaz). Lig iç saha avantajı çevrimiçi
        /// ve daraltılarak öğrenilir: EV_lig = 400·log10(p/(1−p)), p = (ev puanı + k·p0)/(n + k).
        /// </summary>
        public static Dictionary<int, double> EloLogits(IReadOnlyList<HistoricalMatch> ordered, DateTime toUtc, double k = 20, double shrink = 200)
        {
            var rating = new Dictionary<int, double>();
            var league = new Dictionary<int, (double Points, int N)>();
            var result = new Dictionary<int, double>(ordered.Count);
            const double p0 = 0.58; // yalnız daraltma önseli (futbolda ev sahibi puan payı ≈ %58); veri arttıkça etkisi kaybolur
            var i = 0;
            while (i < ordered.Count && ordered[i].KickoffUtc < toUtc)
            {
                var j = i;
                while (j < ordered.Count && ordered[j].KickoffUtc == ordered[i].KickoffUtc) j++;
                for (var t = i; t < j; t++)
                {
                    var m = ordered[t];
                    result[m.MatchId] = Logit(m, rating, league, p0, shrink);
                }
                for (var t = i; t < j; t++)
                {
                    var m = ordered[t];
                    var x = result[m.MatchId];
                    var we = 1 / (1 + Math.Exp(-x));
                    var w = m.HomeGoals > m.AwayGoals ? 1.0 : m.HomeGoals == m.AwayGoals ? 0.5 : 0.0;
                    var mult = Math.Log(Math.Abs(m.HomeGoals - m.AwayGoals) + 1) + 1;
                    rating[m.HomeTeamId] = rating.GetValueOrDefault(m.HomeTeamId, 1500) + k * mult * (w - we);
                    rating[m.AwayTeamId] = rating.GetValueOrDefault(m.AwayTeamId, 1500) - k * mult * (w - we);
                    var l = league.GetValueOrDefault(m.LeagueId);
                    league[m.LeagueId] = (l.Points + w, l.N + 1);
                }
                i = j;
            }
            return result;
        }

        private static double Logit(HistoricalMatch m, Dictionary<int, double> rating, Dictionary<int, (double Points, int N)> league, double p0, double shrink)
        {
            var l = league.GetValueOrDefault(m.LeagueId);
            var p = (l.Points + shrink * p0) / (l.N + shrink);
            var ha = 400 * Math.Log10(p / (1 - p));
            var d = rating.GetValueOrDefault(m.HomeTeamId, 1500) - rating.GetValueOrDefault(m.AwayTeamId, 1500) + ha;
            return d * Math.Log(10) / 400;
        }

        /// <summary>Sıralı logit: P(ev) = σ(x − c), P(dep) = σ(−x − c), P(beraberlik) = kalan. Gol marketleri tahmin edilmez (NaN).</summary>
        public static Pred EloPred(double x, double c)
        {
            var h = Sigmoid(x - c); var a = Sigmoid(-x - c);
            var d = Math.Max(1e-4, 1 - h - a);
            var z = h + d + a;
            return new Pred(h / z, d / z, a / z, double.NaN, double.NaN, double.NaN, double.NaN);
        }

        // ═══════════════════════ DİNLENME GÜNÜ ADAYI ═══════════════════════

        /// <summary>
        /// Dinlenme farkı: dr = kırp(ev dinlenme, 2, 8) − kırp(dep dinlenme, 2, 8) gün; λ_ev ·= e^{β·dr}, λ_dep ·= e^{−β·dr}. Son maç
        /// tarihleri beklentiye maç ÖNCESİ girer (reyting modelinin kendi kaydı). Bilinmeyen dinlenme 0 fark sayılır.
        /// </summary>
        public static OutcomeExpectation RestAdjusted(OutcomeExpectation e, DateTime kickoffUtc, double beta)
        {
            if (beta == 0) return e;
            double Rest(DateTime? last) => last == null ? 5 : Math.Clamp((kickoffUtc - last.Value).TotalDays, 2, 8);
            var dr = Rest(e.HomeLastMatchUtc) - Rest(e.AwayLastMatchUtc);
            return e with { LambdaHome = e.LambdaHome * Math.Exp(beta * dr), LambdaAway = e.LambdaAway * Math.Exp(-beta * dr) };
        }

        // ═══════════════════════ ABLASYON ═══════════════════════

        /// <summary>Üretim bileşenlerinden birini nötrleştirir (kaldırınca iyileşiyorsa bileşen zararlıdır).</summary>
        public static OutcomeModelParameters Ablate(OutcomeModelParameters p, string component)
        {
            var q = p.Clone();
            switch (component)
            {
                case "BaselineMix": q.BaselineMix = 0; break;
                case "UncertaintyMix": q.UncertaintyMix = 0; break;
                case "DrawInflation": q.DrawInflation = 1; q.LeagueDrawInflation.Clear(); break;
                case "GoalScale": q.GoalScale = 1; q.LeagueGoalScale.Clear(); break;
                case "LeagueGoalScale": q.LeagueGoalScale.Clear(); break;
                case "TotalGoalShrink": q.TotalGoalShrink = 1; break;
                default: throw new ArgumentOutOfRangeException(nameof(component), component, null);
            }
            return q;
        }

        public static readonly string[] AblationComponents = { "BaselineMix", "UncertaintyMix", "DrawInflation", "GoalScale", "LeagueGoalScale", "TotalGoalShrink" };

        private static double Sigmoid(double v) => 1 / (1 + Math.Exp(-v));
        private static double Sq(double v) => v * v;
        private static double R(double v) => Math.Round(v, 5);
    }
}
