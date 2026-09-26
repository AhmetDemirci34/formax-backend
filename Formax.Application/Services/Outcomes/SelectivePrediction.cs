using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>
    /// SEÇİCİ TAHMİN (26.09.2026) — "her maça tahmin" yerine tarihsel olarak kanıtlanan güçlü tahminleri ayırmak için ölçüm ve
    /// katman kuralları. Kullanıcıya gösterilen olasılıkları DEĞİŞTİRMEZ; yalnız hangi tahminin güçlü sayılacağını belirler.
    ///
    /// Eşikler YALNIZ test öncesi (eğitim + validation) örneklerden öğrenilir. Final test yalnız değerlendirme içindir.
    /// </summary>
    public static class SelectivePrediction
    {
        public const string SelectorVersion = "selector-1";

        /// <summary>Seçim marketleri. Çifte şans ayrı model değildir: 1X2 dağılımından türetilir.</summary>
        public const string MatchResult = MarketFamilies.MatchResult;
        public const string DoubleChance = MarketFamilies.DoubleChance;
        public static readonly string[] Markets =
        {
            MarketFamilies.MatchResult, MarketFamilies.DoubleChance, MarketFamilies.TotalGoals15, MarketFamilies.TotalGoals25,
            MarketFamilies.TotalGoals35, MarketFamilies.BothTeamsToScore
        };

        /// <summary>KG (BTTS) modeli genel testte lig frekansı tabanından kötü: güçlü katmana ASLA giremez.</summary>
        public static readonly HashSet<string> NeverStrongest = new() { MarketFamilies.BothTeamsToScore };

        public static class Tiers
        {
            public const string Strongest = "Strongest";
            public const string Regular = "Regular";
            public const string Abstain = "Abstain";
        }

        /// <summary>Bir maçta bir market için seçilen sonuç.</summary>
        public sealed record Pick(int MatchId, int LeagueId, DateTime KickoffUtc, string Market, string Outcome, double Probability,
            double Margin, double Entropy, bool? Correct, int HomeSample, int AwaySample, double Coverage)
        {
            /// <summary>Yanılma riski = 1 − seçilen sonucun olasılığı (yüzde puan olarak gösterilir).</summary>
            public double MissRisk => 1 - Probability;
        }

        /// <summary>Market başına seçim: 1X2 en yüksek; çifte şans en yüksek (1X/X2/12); ikili marketlerde ≥ %50 taraf.</summary>
        public static Pick Choose(string market, ScoreDistribution d, EvalSample s) => Choose(market, d, s.MatchId, s.LeagueId, s.KickoffUtc,
            s.HomeGoals, s.AwayGoals, s.E.HomeSample, s.E.AwaySample, s.E.Coverage);

        public static Pick Choose(string market, ScoreDistribution d, int matchId, int leagueId, DateTime kickoffUtc, int? homeGoals, int? awayGoals,
            int homeSample, int awaySample, double coverage)
        {
            double h = d.HomeWin, x = d.Draw, a = d.AwayWin;
            var finished = homeGoals != null && awayGoals != null;
            int hg = homeGoals ?? 0, ag = awayGoals ?? 0;
            switch (market)
            {
                case MarketFamilies.MatchResult:
                {
                    var probs = new[] { ("1", h), ("X", x), ("2", a) }.OrderByDescending(p => p.Item2).ToArray();
                    var o = probs[0].Item1;
                    bool? ok = finished ? (o == "1" ? hg > ag : o == "X" ? hg == ag : hg < ag) : null;
                    return new Pick(matchId, leagueId, kickoffUtc, market, o, probs[0].Item2, probs[0].Item2 - probs[1].Item2, Entropy(h, x, a), ok, homeSample, awaySample, coverage);
                }
                case MarketFamilies.DoubleChance:
                {
                    var probs = new[] { ("1X", h + x), ("X2", x + a), ("12", h + a) }.OrderByDescending(p => p.Item2).ToArray();
                    var o = probs[0].Item1;
                    bool? ok = finished ? (o == "1X" ? hg >= ag : o == "X2" ? hg <= ag : hg != ag) : null;
                    return new Pick(matchId, leagueId, kickoffUtc, market, o, probs[0].Item2, probs[0].Item2 - probs[1].Item2, Entropy(h, x, a), ok, homeSample, awaySample, coverage);
                }
                default:
                {
                    var (q, yes) = market switch
                    {
                        MarketFamilies.TotalGoals15 => (d.Over(1.5), hg + ag > 1),
                        MarketFamilies.TotalGoals25 => (d.Over(2.5), hg + ag > 2),
                        MarketFamilies.TotalGoals35 => (d.Over(3.5), hg + ag > 3),
                        MarketFamilies.BothTeamsToScore => (d.BttsYes, hg > 0 && ag > 0),
                        _ => throw new ArgumentOutOfRangeException(nameof(market))
                    };
                    var over = q >= 0.5;
                    var p = over ? q : 1 - q;
                    var label = market == MarketFamilies.BothTeamsToScore ? (over ? "Yes" : "No") : (over ? "Over" : "Under");
                    bool? ok = finished ? (over == yes) : null;
                    return new Pick(matchId, leagueId, kickoffUtc, market, label, p, 2 * p - 1, Entropy(q, 1 - q), ok, homeSample, awaySample, coverage);
                }
            }
        }

        public static double Entropy(params double[] ps) => -ps.Where(p => p > 0).Sum(p => p * Math.Log(p));

        /// <summary>Wilson skor aralığı (%95). n = 0 ise (0, 1).</summary>
        public static (double Low, double High) Wilson(int correct, int n, double z = 1.959964)
        {
            if (n == 0) return (0, 1);
            var p = (double)correct / n;
            var den = 1 + z * z / n;
            var centre = (p + z * z / (2 * n)) / den;
            var half = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n)) / den;
            return (Math.Max(0, centre - half), Math.Min(1, centre + half));
        }

        public sealed class Row
        {
            public string Label { get; set; } = string.Empty;
            public int N { get; set; }
            public int Correct { get; set; }
            public int Wrong => N - Correct;
            public double Accuracy { get; set; }
            public double MeanProbability { get; set; }
            /// <summary>Ortalama tahmin − gerçek doğruluk (pozitif = aşırı güven).</summary>
            public double CalibrationGap { get; set; }
            public double CiLow { get; set; }
            public double CiHigh { get; set; }
            public double MinProbability { get; set; }
        }

        public static Row Summarize(string label, IReadOnlyList<Pick> picks)
        {
            var n = picks.Count; var c = picks.Count(p => p.Correct == true);
            var (lo, hi) = Wilson(c, n);
            var acc = n == 0 ? 0 : (double)c / n;
            var mean = n == 0 ? 0 : picks.Average(p => p.Probability);
            return new Row
            {
                Label = label, N = n, Correct = c, Accuracy = R(acc), MeanProbability = R(mean), CalibrationGap = R(mean - acc),
                CiLow = R(lo), CiHigh = R(hi), MinProbability = n == 0 ? 0 : R(picks.Min(p => p.Probability))
            };
        }

        public static readonly double[] CoverageLevels = { 0.01, 0.02, 0.05, 0.10, 0.20, 0.30, 1.0 };

        /// <summary>En güçlüden en zayıfa (olasılık, sonra fark, sonra MatchId) sıralı kapsam tablosu.</summary>
        public static List<Row> PrecisionCoverage(IReadOnlyList<Pick> picks)
        {
            var ordered = picks.OrderByDescending(p => p.Probability).ThenByDescending(p => p.Margin).ThenBy(p => p.MatchId).ToList();
            return CoverageLevels.Select(level =>
            {
                var k = level >= 1 ? ordered.Count : Math.Max(1, (int)Math.Round(ordered.Count * level));
                return Summarize(level >= 1 ? "Tüm tahminler" : $"En güçlü %{level * 100:0}", ordered.Take(Math.Min(k, ordered.Count)).ToList());
            }).ToList();
        }

        public static readonly (double Lo, double Hi, string Label)[] Bands =
        {
            (0.50, 0.60, "%50–59"), (0.60, 0.70, "%60–69"), (0.70, 0.80, "%70–79"), (0.80, 0.85, "%80–84"), (0.85, 0.90, "%85–89"), (0.90, 1.01, "%90+")
        };

        public static List<Row> CalibrationBands(IReadOnlyList<Pick> picks)
            => Bands.Select(b => Summarize(b.Label, picks.Where(p => p.Probability >= b.Lo && p.Probability < b.Hi).ToList())).ToList();

        // ═══════════════════════ EŞİK ÖĞRENİCİ (yalnız test öncesi) ═══════════════════════

        /// <summary>
        /// İLERİYE DÖNÜK GÖLGE SEÇİCİSİ — 26.09.2026'da B kuralıyla test öncesi örneklerden öğrenildi ve ÖNCEDEN KAYDEDİLDİ:
        /// yalnız çifte şans p ≥ 0,84 güçlü sayılır (diğer marketlerde kanıtlı eşik yok). Kullanıcıya rozet olarak GÖSTERİLMEZ;
        /// yalnız gölge defterinde katman etiketi olarak tutulur ve ileride taze maçlarla doğrulanır.
        /// </summary>
        public const string ForwardSelectorVersion = "selector-forward-1";
        public static readonly IReadOnlyDictionary<string, double?> ForwardThresholds = new Dictionary<string, double?>
        {
            [MarketFamilies.MatchResult] = null, [MarketFamilies.DoubleChance] = 0.84, [MarketFamilies.TotalGoals15] = null,
            [MarketFamilies.TotalGoals25] = null, [MarketFamilies.TotalGoals35] = null, [MarketFamilies.BothTeamsToScore] = null
        };

        public const double TargetWilsonLow = 0.85;
        public const int MinLearnSamples = 50;

        /// <summary>
        /// Market başına güçlü eşik: test ÖNCESİ örneklerde p ≥ t olan tahminlerin Wilson alt sınırı ≥ %85 ve en az 50 örnek olan
        /// EN KÜÇÜK t (ızgara 0,60…0,97). Böyle t yoksa market güçlü katmana giremez (null). KG hiçbir koşulda giremez.
        /// </summary>
        public static Dictionary<string, double?> LearnThresholds(IReadOnlyList<Pick> preTest)
        {
            var result = new Dictionary<string, double?>();
            foreach (var market in Markets)
            {
                result[market] = null;
                if (NeverStrongest.Contains(market)) continue;
                var ps = preTest.Where(p => p.Market == market).ToList();
                for (var t = 0.60; t <= 0.9701; t += 0.01)
                {
                    var tt = Math.Round(t, 2);
                    var above = ps.Where(p => p.Probability >= tt).ToList();
                    if (above.Count < MinLearnSamples) break;
                    var (lo, _) = Wilson(above.Count(p => p.Correct == true), above.Count);
                    if (lo >= TargetWilsonLow) { result[market] = tt; break; }
                }
            }
            return result;
        }

        /// <summary>
        /// B KURALI (ileriye dönük gölge için 26.09.2026'da önceden kayıtlı) — final kapıyla uyumlu: test öncesi örneklerde p ≥ t
        /// olan tahminlerin doğruluğu ≥ %90, Wilson alt sınırı ≥ %80 ve en az 50 örnek olan EN KÜÇÜK t. Final test bu kural
        /// öncesinde görüldüğü için bu kuralın kanıtı yalnız ileriye dönük kayıttan gelir.
        /// </summary>
        public static Dictionary<string, double?> LearnThresholdsForward(IReadOnlyList<Pick> preTest)
        {
            var result = new Dictionary<string, double?>();
            foreach (var market in Markets)
            {
                result[market] = null;
                if (NeverStrongest.Contains(market)) continue;
                var ps = preTest.Where(p => p.Market == market).ToList();
                for (var t = 0.60; t <= 0.9701; t += 0.01)
                {
                    var tt = Math.Round(t, 2);
                    var above = ps.Where(p => p.Probability >= tt).ToList();
                    if (above.Count < MinLearnSamples) break;
                    var c = above.Count(p => p.Correct == true);
                    var (lo, _) = Wilson(c, above.Count);
                    if ((double)c / above.Count >= 0.90 && lo >= 0.80) { result[market] = tt; break; }
                }
            }
            return result;
        }

        /// <summary>
        /// Maç katmanı: tahmin üretilemediyse Abstain; eşiği geçen (güçlü katmana izinli) bir market varsa Strongest ve en yüksek
        /// olasılıklı o seçim; yoksa Regular. Abstain bir başarısızlık değildir.
        /// </summary>
        public static (string Tier, Pick? Strongest) Tier(IReadOnlyList<Pick> matchPicks, IReadOnlyDictionary<string, double?> thresholds, bool sufficient)
        {
            if (!sufficient || matchPicks.Count == 0) return (Tiers.Abstain, null);
            var strong = matchPicks.Where(p => !NeverStrongest.Contains(p.Market) && thresholds.TryGetValue(p.Market, out var t) && t != null && p.Probability >= t)
                .OrderByDescending(p => p.Probability).ThenBy(p => p.Market, StringComparer.Ordinal).FirstOrDefault();
            return strong == null ? (Tiers.Regular, null) : (Tiers.Strongest, strong);
        }

        private static double R(double v) => Math.Round(v, 4);
    }

    /// <summary>
    /// BAĞIMSIZ DİNAMİK ELO — lig bazlı iç saha avantajı çevrimiçi öğrenilir. Aynı başlama saatindeki maçlar önce birlikte
    /// tahmin edilir, sonra birlikte güncellenir. Model 5 gölge adayının tek ek bileşeni.
    /// </summary>
    public sealed class DynamicElo
    {
        public const double K = 20;
        public const double Shrink = 200;
        public const double HomeShareDefault = 0.58;
        private readonly Dictionary<int, double> _rating = new();
        private readonly Dictionary<int, (double Points, int N)> _league = new();

        public double Logit(int leagueId, int homeTeamId, int awayTeamId)
        {
            var l = _league.GetValueOrDefault(leagueId);
            var p = (l.Points + Shrink * HomeShareDefault) / (l.N + Shrink);
            var ha = 400 * Math.Log10(p / (1 - p));
            var d = _rating.GetValueOrDefault(homeTeamId, 1500) - _rating.GetValueOrDefault(awayTeamId, 1500) + ha;
            return d * Math.Log(10) / 400;
        }

        /// <summary>Aynı başlama saatindeki maç grubunu işler (önceden hesaplanan logitlerle).</summary>
        public void UpdateBatch(IReadOnlyList<(HistoricalMatch Match, double Logit)> batch)
        {
            foreach (var (m, x) in batch)
            {
                var we = 1 / (1 + Math.Exp(-x));
                var w = m.HomeGoals > m.AwayGoals ? 1.0 : m.HomeGoals == m.AwayGoals ? 0.5 : 0.0;
                var mult = Math.Log(Math.Abs(m.HomeGoals - m.AwayGoals) + 1) + 1;
                _rating[m.HomeTeamId] = _rating.GetValueOrDefault(m.HomeTeamId, 1500) + K * mult * (w - we);
                _rating[m.AwayTeamId] = _rating.GetValueOrDefault(m.AwayTeamId, 1500) - K * mult * (w - we);
                var l = _league.GetValueOrDefault(m.LeagueId);
                _league[m.LeagueId] = (l.Points + w, l.N + 1);
            }
        }

        /// <summary>Geçmişi sırayla işler; <paramref name="onPredict"/> her maç için güncellemeden ÖNCEKİ logiti alır.</summary>
        public static DynamicElo Replay(IReadOnlyList<HistoricalMatch> ordered, DateTime toUtc, Action<HistoricalMatch, double>? onPredict = null)
        {
            var elo = new DynamicElo();
            var i = 0;
            while (i < ordered.Count && ordered[i].KickoffUtc < toUtc)
            {
                var j = i;
                while (j < ordered.Count && ordered[j].KickoffUtc == ordered[i].KickoffUtc) j++;
                var batch = new List<(HistoricalMatch, double)>(j - i);
                for (var k = i; k < j; k++)
                {
                    var x = elo.Logit(ordered[k].LeagueId, ordered[k].HomeTeamId, ordered[k].AwayTeamId);
                    onPredict?.Invoke(ordered[k], x);
                    batch.Add((ordered[k], x));
                }
                elo.UpdateBatch(batch);
                i = j;
            }
            return elo;
        }
    }

    /// <summary>
    /// MODEL 5 GÖLGE (formax-outcome-5-shadow) — ÖNCEDEN KAYITLI kurallar (26.09.2026), final test sonucuna göre yeniden
    /// AYARLANMAZ: 4.0 kalibre skor matrisi, bağımsız dinamik Elo'nun sıralı-logit 1X2'si ile w = 0,30, c = 0,65 karıştırılır
    /// (sonuç sınıfı yeniden ağırlıklandırma). YALNIZ yerel lig maçında; UEFA organizasyonları ve ligler arası maçta 4.0 aynen
    /// kalır. Kullanıcıya gösterilmez; yalnız ileriye dönük kilitli kayıt ve puanlama içindir.
    /// </summary>
    public static class Model5Shadow
    {
        public const string Version = "formax-outcome-5-shadow";
        public const double EloWeight = 0.30;
        public const double EloDrawC = 0.65;
        public static readonly HashSet<int> NotApplied = new() { 2, 3, 848 };

        public static string ConfigHash { get; } = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            Version, OutcomeModelVersion.Current, EloWeight.ToString("R", CultureInfo.InvariantCulture), EloDrawC.ToString("R", CultureInfo.InvariantCulture),
            DynamicElo.K.ToString(CultureInfo.InvariantCulture), DynamicElo.Shrink.ToString(CultureInfo.InvariantCulture),
            DynamicElo.HomeShareDefault.ToString("R", CultureInfo.InvariantCulture), string.Join(",", NotApplied.OrderBy(x => x)), OutcomeBacktest.MethodVersion))))
            .ToLowerInvariant()[..32];

        public static bool Applies(int leagueId, bool crossLeague) => !crossLeague && !NotApplied.Contains(leagueId);

        public static ScoreDistribution Predict(ScoreDistribution model40, int leagueId, bool crossLeague, double eloLogit)
        {
            if (!Applies(leagueId, crossLeague)) return model40;
            var h = 1 / (1 + Math.Exp(-(eloLogit - EloDrawC)));
            var a = 1 / (1 + Math.Exp(-(-eloLogit - EloDrawC)));
            var d = Math.Max(1e-4, 1 - h - a);
            var z = h + d + a;
            h /= z; d /= z; a /= z;
            var w = EloWeight;
            return model40.ReweightResult((1 - w) * model40.HomeWin + w * h, (1 - w) * model40.Draw + w * d, (1 - w) * model40.AwayWin + w * a);
        }
    }
}
