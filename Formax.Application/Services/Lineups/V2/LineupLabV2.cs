using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;

namespace Formax.Application.Services.Lineups.V2
{
    /// <summary>Laboratuvar sürümü — koşu kaydı ve config özeti bu sabitle etiketlenir.</summary>
    public static class LineupLabVersion
    {
        public const string Current = "lineup-lab-v2";
    }

    /// <summary>Bir adayın hangi özellik ailelerini KULLANDIĞI. Kapalı aile gerçekten hesaba girmez.</summary>
    public sealed record LineupFeatureSet(
        string Name,
        string Description,
        bool Continuity = false,
        bool Goalkeeper = false,
        bool PositionalUnits = false,
        bool RoleShare = false,
        bool MinuteShare = false,
        bool Replacement = false)
    {
        /// <summary>Tek tarafın özellik vektörü. Kapalı aileler vektöre HİÇ eklenmez (sıfırla doldurulmaz).</summary>
        public double[] Build(LineupSideFeatures own, LineupSideFeatures opponent)
        {
            var v = new List<double>(24);
            void Add(Func<LineupSideFeatures, double> f) { v.Add(f(own)); v.Add(f(opponent)); }

            if (Continuity)
            {
                Add(s => s.StartingXiContinuity);
                Add(s => s.StarterJaccard);
                Add(s => s.MissingExpectedStarters);
                Add(s => s.AddedUnexpectedStarters);
            }
            if (Goalkeeper) Add(s => s.GoalkeeperChanged ? 1 : 0);
            if (PositionalUnits)
            {
                Add(s => s.DefensiveUnitChanges);
                Add(s => s.MidfieldUnitChanges);
                Add(s => s.AttackingUnitChanges);
            }
            if (RoleShare)
            {
                Add(s => s.MeanStarterStartShare);
                Add(s => s.WeightedStarterShare);
                Add(s => s.ReturningRegulars);
            }
            // Dakika payı YALNIZ gerçekten varsa; yoksa 0 değil, ailenin katkısı 0 ve bayrak 0 olur.
            if (MinuteShare)
            {
                Add(s => s.MeanStarterMinuteShare ?? 0);
                Add(s => s.MeanStarterMinuteShare.HasValue ? 1 : 0);
            }
            if (Replacement)
            {
                Add(s => s.ReplacementDeltaStartShare);
                Add(s => s.ReplacementStartShare);
                Add(s => s.ReplacementExperience);
            }
            return v.ToArray();
        }

        public int Dimension => Build(LineupSideFeatures.Unavailable, LineupSideFeatures.Unavailable).Length;

        public static readonly LineupFeatureSet Continuity1 =
            new("C1_Continuity", "Yalnız beklenen/açıklanan ilk 11 farkı + kaleci + mevki grubu", Continuity: true, Goalkeeper: true, PositionalUnits: true);
        public static readonly LineupFeatureSet Role2 =
            new("C2_RoleShare", "Yalnız rol/kullanım payı (başlangıç payı, ağırlıklı pay, dönen düzenli)", RoleShare: true);
        public static readonly LineupFeatureSet Replacement3 =
            new("C3_ReplacementDelta", "Yalnız eksik beklenen oyuncu ile yerine başlayanın profil farkı", Replacement: true);
        public static readonly LineupFeatureSet Full5 =
            new("C5_Combined", "Birleşik: devamlılık + kaleci + mevki + rol/pay + dakika payı + yedek farkı",
                Continuity: true, Goalkeeper: true, PositionalUnits: true, RoleShare: true, MinuteShare: true, Replacement: true);

        // Ablasyonlar (H): her biri yalnız KENDİ ailesini açar.
        public static readonly LineupFeatureSet OnlyContinuity = new("H2_Continuity", "Base + continuity", Continuity: true);
        public static readonly LineupFeatureSet OnlyGoalkeeper = new("H3_Goalkeeper", "Base + kaleci değişimi", Goalkeeper: true);
        public static readonly LineupFeatureSet OnlyUnits = new("H4_PositionalUnits", "Base + mevki grubu değişimleri", PositionalUnits: true);
        public static readonly LineupFeatureSet OnlyRole = new("H5_RoleStartShare", "Base + rol/başlangıç payı", RoleShare: true);
        public static readonly LineupFeatureSet OnlyMinutes = new("H6_MinuteShare", "Base + dakika payı", MinuteShare: true);
        public static readonly LineupFeatureSet OnlyReplacement = new("H7_ReplacementDelta", "Base + yedek farkı", Replacement: true);
    }

    /// <summary>Tek maçın laboratuvar örneği: taban beklenti + gerçek sonuç + kadro özellikleri.</summary>
    public sealed record LabSample(
        int MatchId, DateTime KickoffUtc, int LeagueId,
        OutcomeExpectation Expectation, int HomeGoals, int AwayGoals,
        ScoreDistribution BaseDistribution, ScoreDistribution BaselineDistribution,
        LineupMatchFeatures? Features);

    /// <summary>Bir walk-forward fold'u.</summary>
    public sealed record LabFold(string Name, DateTime StartUtc, DateTime EndUtc, bool IsHoldout);

    public sealed class LabMetric
    {
        public string Scope { get; set; } = string.Empty;   // "Pooled" | "PL" | "SerieA" | fold adı
        public string Family { get; set; } = string.Empty;
        public int Matches { get; set; }
        public int MatchesWithLineup { get; set; }
        public int AdjustedMatches { get; set; }
        public double Coverage { get; set; }
        public double BaseLogLoss { get; set; }
        public double LogLoss { get; set; }
        public double BaseBrier { get; set; }
        public double Brier { get; set; }
        public double BaseEce { get; set; }
        public double Ece { get; set; }
        /// <summary>Aday − taban, YALNIZ düzeltilmiş maçlarda (seyreltilmemiş etki).</summary>
        public double AdjustedOnlyDiff { get; set; }
        public double CiLow { get; set; }
        public double CiHigh { get; set; }
        public bool SignificantlyBetter => CiHigh < 0;
        /// <summary>Ortalama mutlak olasılık değişimi (1X2'de üç bileşenin ortalaması).</summary>
        public double MeanAbsProbabilityChange { get; set; }
        /// <summary>Düzeltmenin doğru yönde olduğu maç oranı (gerçekleşen sonucun olasılığı arttı mı).</summary>
        public double DirectionalAccuracy { get; set; }
        public double CalibrationSlope { get; set; }
        public double CalibrationIntercept { get; set; }
    }

    public sealed class LabCandidateResult
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Dimension { get; set; }
        public double SelectedRidge { get; set; }
        public int AdjustedMatches { get; set; }
        public double MeanAbsDelta { get; set; }
        public double P50Delta { get; set; }
        public double P90Delta { get; set; }
        public double P95Delta { get; set; }
        public double MaxDelta { get; set; }
        /// <summary>Tavanın gerçekten devreye girdiği taraf-satır oranı.</summary>
        public double ClampRate { get; set; }
        public List<LabMetric> Metrics { get; set; } = new();
        /// <summary>Katsayı işaretlerinin fold'lar arası kararlılığı (1 = hep aynı işaret).</summary>
        public double CoefficientSignStability { get; set; }
        public List<string> Notes { get; set; } = new();

        public LabMetric? Metric(string scope, string family)
            => Metrics.FirstOrDefault(m => m.Scope == scope && m.Family == family);
    }

    /// <summary>
    /// PLAYER IMPACT V2 LABORATUVARI — zamansal (walk-forward) deney.
    ///
    /// TABAN DONDURULUR: <see cref="LabSample.BaseDistribution"/> deney başında bir kez üretilir ve
    /// bütün adaylar AYNI örnek kümesi üzerinde onunla karşılaştırılır. Aday lehine maç seçilmez.
    ///
    /// Her fold için katsayılar YALNIZ fold başlangıcından ÖNCE başlamış maçlardan öğrenilir;
    /// ceza gücü doğrulama fold'larında seçilir, holdout'a bakılarak DEĞİL.
    /// </summary>
    public static class LineupLabV2
    {
        /// <summary>Doğrulamada denenen ceza güçleri (ridge).</summary>
        public static readonly double[] RidgeGrid = { 1, 3, 10, 30, 100, 300, 1000 };

        /// <summary>Bir fold'da katsayı öğrenmek için gereken en az eğitim satırı (taraf-maç).</summary>
        public const int MinTrainingRows = 200;

        public sealed record LabRequest(
            IReadOnlyList<LabSample> Samples,
            IReadOnlyList<LabFold> Folds,
            IReadOnlyList<LineupFeatureSet> Candidates,
            int BootstrapSeed = 20260919);

        public sealed class LabReport
        {
            public string Version { get; set; } = LineupLabVersion.Current;
            public string ConfigHash { get; set; } = string.Empty;
            public DateTime GeneratedAtUtc { get; set; }
            public int Samples { get; set; }
            public int SamplesWithLineup { get; set; }
            public List<LabFold> Folds { get; set; } = new();
            public List<LabCandidateResult> Candidates { get; set; } = new();
            public List<string> Notes { get; set; } = new();
        }

        /// <summary>
        /// Adayı bütün fold'larda değerlendirir. Her fold'da:
        ///  1. eğitim = fold başlangıcından ÖNCE başlamış, kadrosu kullanılabilir maçlar,
        ///  2. ceza gücü doğrulama fold'larının ortalama kaybına göre seçilir (holdout hariç),
        ///  3. tahmin yalnız fold içindeki maçlara uygulanır.
        /// </summary>
        /// <param name="trainingFilter">
        /// EĞİTİM kümesini daraltır (ör. yalnız Premier League maçlarından öğren). Değerlendirme
        /// kümesi DEĞİŞMEZ — ligler arası genelleme testi böyle yapılır: bir ligde öğren, ötekinde ölç.
        /// </param>
        public static LabCandidateResult Evaluate(
            LabRequest request, LineupFeatureSet featureSet, double? forcedRidge = null,
            Func<LabSample, LineupMatchFeatures?>? featureOverride = null,
            Func<LabSample, bool>? trainingFilter = null)
        {
            var samples = request.Samples.OrderBy(s => s.KickoffUtc).ThenBy(s => s.MatchId).ToList();
            LineupMatchFeatures? Feat(LabSample s) => featureOverride != null ? featureOverride(s) : s.Features;
            LineupMatchFeatures? TrainFeat(LabSample s) => trainingFilter != null && !trainingFilter(s) ? null : Feat(s);

            var result = new LabCandidateResult
            {
                Name = featureSet.Name,
                Description = featureSet.Description,
                Dimension = featureSet.Dimension
            };

            // ── 1. CEZA GÜCÜ SEÇİMİ — yalnız DOĞRULAMA fold'larında ────────────────────
            var validation = request.Folds.Where(f => !f.IsHoldout).ToList();
            var ridge = forcedRidge ?? SelectRidge(samples, validation, featureSet, Feat, TrainFeat);
            result.SelectedRidge = ridge;

            // ── 2. HER FOLD: eğit → uygula ─────────────────────────────────────────────
            var adjusted = new Dictionary<int, ScoreDistribution>();
            var deltas = new List<double>();
            var clampChecks = 0;
            var clamped = 0;
            var signVectors = new List<double[]>();

            foreach (var fold in request.Folds)
            {
                var train = TrainingRows(samples, fold.StartUtc, featureSet, TrainFeat);
                if (train.Count < MinTrainingRows)
                {
                    result.Notes.Add($"{fold.Name}: eğitim satırı {train.Count} < {MinTrainingRows} — katsayı öğrenilmedi, delta 0.");
                    continue;
                }
                var model = LineupPoissonAdjuster.Fit(train, ridge);
                signVectors.Add(model.Coefficients.Select(c => Math.Sign(c) * 1.0).ToArray());

                foreach (var s in samples.Where(s => s.KickoffUtc >= fold.StartUtc && s.KickoffUtc < fold.EndUtc))
                {
                    var f = Feat(s);
                    if (f is not { Usable: true }) continue;
                    var xHome = featureSet.Build(f.Home, f.Away);
                    var xAway = featureSet.Build(f.Away, f.Home);
                    clampChecks += 2;
                    if (model.WouldClamp(xHome)) clamped++;
                    if (model.WouldClamp(xAway)) clamped++;

                    var dHome = model.LogDelta(xHome);
                    var dAway = model.LogDelta(xAway);
                    if (Math.Abs(dHome) < 1e-9 && Math.Abs(dAway) < 1e-9) continue;

                    deltas.Add(Math.Max(Math.Abs(dHome), Math.Abs(dAway)));
                    adjusted[s.MatchId] = ScoreDistribution.Poisson(
                        Math.Max(0.01, s.Expectation.LambdaHome * Math.Exp(dHome)),
                        Math.Max(0.01, s.Expectation.LambdaAway * Math.Exp(dAway)));
                }
            }

            result.AdjustedMatches = adjusted.Count;
            result.ClampRate = clampChecks == 0 ? 0 : Math.Round(clamped / (double)clampChecks, 4);
            if (deltas.Count > 0)
            {
                var sorted = deltas.OrderBy(d => d).ToList();
                result.MeanAbsDelta = Math.Round(sorted.Average(), 6);
                result.P50Delta = Math.Round(Percentile(sorted, 0.50), 6);
                result.P90Delta = Math.Round(Percentile(sorted, 0.90), 6);
                result.P95Delta = Math.Round(Percentile(sorted, 0.95), 6);
                result.MaxDelta = Math.Round(sorted[^1], 6);
            }
            result.CoefficientSignStability = SignStability(signVectors);

            // ── 3. METRİKLER ───────────────────────────────────────────────────────────
            ScoreDistribution Dist(LabSample s) => adjusted.GetValueOrDefault(s.MatchId, s.BaseDistribution);

            void Measure(string scope, IReadOnlyList<LabSample> subset)
            {
                if (subset.Count == 0) return;
                foreach (var family in MarketFamilies.Measured)
                    result.Metrics.Add(Measurement(scope, family, subset, Dist, adjusted, request.BootstrapSeed));
            }

            var evaluated = samples.Where(s => request.Folds.Any(f => s.KickoffUtc >= f.StartUtc && s.KickoffUtc < f.EndUtc)).ToList();
            Measure("Pooled", evaluated);
            Measure("PL", evaluated.Where(s => s.LeagueId == 39).ToList());
            Measure("SerieA", evaluated.Where(s => s.LeagueId == 135).ToList());
            foreach (var fold in request.Folds)
                Measure(fold.Name, evaluated.Where(s => s.KickoffUtc >= fold.StartUtc && s.KickoffUtc < fold.EndUtc).ToList());

            return result;
        }

        /// <summary>Ceza gücü: doğrulama fold'larının TOPLAM 1X2 log kaybını en küçükleyen değer.</summary>
        private static double SelectRidge(
            IReadOnlyList<LabSample> samples, IReadOnlyList<LabFold> validation,
            LineupFeatureSet featureSet, Func<LabSample, LineupMatchFeatures?> feat, Func<LabSample, LineupMatchFeatures?> trainFeat)
        {
            var best = RidgeGrid[^1];
            var bestLoss = double.MaxValue;
            foreach (var ridge in RidgeGrid)
            {
                double loss = 0; var n = 0;
                foreach (var fold in validation)
                {
                    var train = TrainingRows(samples, fold.StartUtc, featureSet, trainFeat);
                    if (train.Count < MinTrainingRows) continue;
                    var model = LineupPoissonAdjuster.Fit(train, ridge);
                    foreach (var s in samples.Where(s => s.KickoffUtc >= fold.StartUtc && s.KickoffUtc < fold.EndUtc))
                    {
                        var f = feat(s);
                        if (f is not { Usable: true }) continue;
                        var d = ScoreDistribution.Poisson(
                            Math.Max(0.01, s.Expectation.LambdaHome * Math.Exp(model.LogDelta(featureSet.Build(f.Home, f.Away)))),
                            Math.Max(0.01, s.Expectation.LambdaAway * Math.Exp(model.LogDelta(featureSet.Build(f.Away, f.Home)))));
                        loss += GroupEvaluator.ResultLoss(d.HomeWin, d.Draw, d.AwayWin, s.HomeGoals, s.AwayGoals);
                        n++;
                    }
                }
                if (n == 0) continue;
                var mean = loss / n;
                if (mean < bestLoss) { bestLoss = mean; best = ridge; }
            }
            return best;
        }

        /// <summary>Kesimden ÖNCE başlamış, kadrosu kullanılabilir maçların taraf satırları.</summary>
        private static List<PoissonRow> TrainingRows(
            IReadOnlyList<LabSample> samples, DateTime cutoffUtc,
            LineupFeatureSet featureSet, Func<LabSample, LineupMatchFeatures?> feat)
        {
            var rows = new List<PoissonRow>();
            foreach (var s in samples)
            {
                if (s.KickoffUtc >= cutoffUtc) break;          // KESİN cutoff: aynı maç hem eğitim hem test olamaz
                var f = feat(s);
                if (f is not { Usable: true }) continue;
                rows.Add(new PoissonRow(featureSet.Build(f.Home, f.Away), s.Expectation.LambdaHome, s.HomeGoals));
                rows.Add(new PoissonRow(featureSet.Build(f.Away, f.Home), s.Expectation.LambdaAway, s.AwayGoals));
            }
            return rows;
        }

        private static LabMetric Measurement(
            string scope, string family, IReadOnlyList<LabSample> subset,
            Func<LabSample, ScoreDistribution> dist, IReadOnlyDictionary<int, ScoreDistribution> adjusted, int seed)
        {
            var m = new LabMetric
            {
                Scope = scope, Family = family, Matches = subset.Count,
                MatchesWithLineup = subset.Count(s => s.Features is { Usable: true }),
                AdjustedMatches = subset.Count(s => adjusted.ContainsKey(s.MatchId))
            };
            m.Coverage = subset.Count == 0 ? 0 : Math.Round(m.AdjustedMatches / (double)subset.Count, 4);

            double ll = 0, bll = 0, br = 0, bbr = 0;
            var pooledCand = new List<(double P, bool Y)>();
            var pooledBase = new List<(double P, bool Y)>();
            var diffs = new List<double>();
            var absChange = new List<double>();
            var correct = 0; var directional = 0;

            foreach (var s in subset)
            {
                var c = dist(s);
                var b = s.BaseDistribution;
                ll += Loss(family, c, s); bll += Loss(family, b, s);
                br += Sq(family, c, s); bbr += Sq(family, b, s);
                Pool(family, c, s, pooledCand);
                Pool(family, b, s, pooledBase);

                if (!adjusted.ContainsKey(s.MatchId)) continue;
                var dl = Loss(family, c, s) - Loss(family, b, s);
                diffs.Add(dl);
                var pc = Probability(family, c, s); var pb = Probability(family, b, s);
                absChange.Add(Math.Abs(pc - pb));
                directional++;
                if (pc > pb) correct++;   // gerçekleşen sonucun olasılığı ARTTIYSA yön doğru
            }

            var n = Math.Max(1, subset.Count);
            m.LogLoss = R(ll / n); m.BaseLogLoss = R(bll / n);
            m.Brier = R(br / n); m.BaseBrier = R(bbr / n);
            m.Ece = R(OutcomeBacktest.Ece(pooledCand, 10));
            m.BaseEce = R(OutcomeBacktest.Ece(pooledBase, 10));
            m.AdjustedOnlyDiff = diffs.Count == 0 ? 0 : R(diffs.Average());
            var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs.ToArray(), EligibilityPolicy.BootstrapSamples, seed);
            m.CiLow = R(lo); m.CiHigh = R(hi);
            m.MeanAbsProbabilityChange = absChange.Count == 0 ? 0 : R(absChange.Average());
            m.DirectionalAccuracy = directional == 0 ? 0 : R(correct / (double)directional);
            var (slope, intercept) = CalibrationLine(pooledCand);
            m.CalibrationSlope = R(slope); m.CalibrationIntercept = R(intercept);
            return m;
        }

        // ── Market yardımcıları ────────────────────────────────────────────────────────

        private static double Loss(string family, ScoreDistribution d, LabSample s) => family switch
        {
            MarketFamilies.MatchResult => GroupEvaluator.ResultLoss(d.HomeWin, d.Draw, d.AwayWin, s.HomeGoals, s.AwayGoals),
            MarketFamilies.TotalGoals15 => GroupEvaluator.BinLoss(d.Over(1.5), s.HomeGoals + s.AwayGoals > 1),
            MarketFamilies.TotalGoals25 => GroupEvaluator.BinLoss(d.Over(2.5), s.HomeGoals + s.AwayGoals > 2),
            MarketFamilies.TotalGoals35 => GroupEvaluator.BinLoss(d.Over(3.5), s.HomeGoals + s.AwayGoals > 3),
            MarketFamilies.BothTeamsToScore => GroupEvaluator.BinLoss(d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0),
            _ => 0
        };

        /// <summary>GERÇEKLEŞEN sonuca verilen olasılık (yön doğruluğu için).</summary>
        private static double Probability(string family, ScoreDistribution d, LabSample s) => family switch
        {
            MarketFamilies.MatchResult => s.HomeGoals > s.AwayGoals ? d.HomeWin : s.HomeGoals == s.AwayGoals ? d.Draw : d.AwayWin,
            MarketFamilies.TotalGoals15 => s.HomeGoals + s.AwayGoals > 1 ? d.Over(1.5) : d.Under(1.5),
            MarketFamilies.TotalGoals25 => s.HomeGoals + s.AwayGoals > 2 ? d.Over(2.5) : d.Under(2.5),
            MarketFamilies.TotalGoals35 => s.HomeGoals + s.AwayGoals > 3 ? d.Over(3.5) : d.Under(3.5),
            MarketFamilies.BothTeamsToScore => s.HomeGoals > 0 && s.AwayGoals > 0 ? d.BttsYes : d.BttsNo,
            _ => 0
        };

        private static double Sq(string family, ScoreDistribution d, LabSample s)
        {
            if (family == MarketFamilies.MatchResult)
            {
                double hw = s.HomeGoals > s.AwayGoals ? 1 : 0, dr = s.HomeGoals == s.AwayGoals ? 1 : 0, aw = s.HomeGoals < s.AwayGoals ? 1 : 0;
                return Math.Pow(d.HomeWin - hw, 2) + Math.Pow(d.Draw - dr, 2) + Math.Pow(d.AwayWin - aw, 2);
            }
            var p = Probability(family, d, s);
            return Math.Pow(1 - p, 2);
        }

        private static void Pool(string family, ScoreDistribution d, LabSample s, List<(double, bool)> pool)
        {
            if (family == MarketFamilies.MatchResult)
            {
                pool.Add((d.HomeWin, s.HomeGoals > s.AwayGoals));
                pool.Add((d.Draw, s.HomeGoals == s.AwayGoals));
                pool.Add((d.AwayWin, s.HomeGoals < s.AwayGoals));
                return;
            }
            var (p, y) = family switch
            {
                MarketFamilies.TotalGoals15 => (d.Over(1.5), s.HomeGoals + s.AwayGoals > 1),
                MarketFamilies.TotalGoals25 => (d.Over(2.5), s.HomeGoals + s.AwayGoals > 2),
                MarketFamilies.TotalGoals35 => (d.Over(3.5), s.HomeGoals + s.AwayGoals > 3),
                _ => (d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0)
            };
            pool.Add((p, y));
            pool.Add((1 - p, !y));
        }

        /// <summary>Kalibrasyon doğrusu: gerçekleşme ~ a + b·tahmin. İdeal b=1, a=0.</summary>
        private static (double Slope, double Intercept) CalibrationLine(IReadOnlyList<(double P, bool Y)> pool)
        {
            if (pool.Count < 2) return (0, 0);
            var mx = pool.Average(p => p.P);
            var my = pool.Average(p => p.Y ? 1.0 : 0.0);
            double num = 0, den = 0;
            foreach (var (p, y) in pool) { num += (p - mx) * ((y ? 1.0 : 0.0) - my); den += (p - mx) * (p - mx); }
            if (den <= 1e-12) return (0, my);
            var slope = num / den;
            return (slope, my - slope * mx);
        }

        private static double Percentile(IReadOnlyList<double> sorted, double q)
        {
            if (sorted.Count == 0) return 0;
            var idx = (int)Math.Clamp(Math.Round(q * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }

        /// <summary>Katsayı işaretlerinin fold'lar arası kararlılığı: boyut başına çoğunluk işaretinin payı.</summary>
        private static double SignStability(IReadOnlyList<double[]> signVectors)
        {
            if (signVectors.Count < 2) return 0;
            var d = signVectors[0].Length;
            if (d == 0) return 0;
            double total = 0;
            for (var j = 0; j < d; j++)
            {
                var pos = signVectors.Count(v => v[j] > 0);
                var neg = signVectors.Count(v => v[j] < 0);
                total += Math.Max(pos, neg) / (double)signVectors.Count;
            }
            return Math.Round(total / d, 4);
        }

        private static double R(double v) => Math.Round(v, 6);
    }
}
