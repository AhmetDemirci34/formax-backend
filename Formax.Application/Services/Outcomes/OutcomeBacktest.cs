using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>Beklenen kalibrasyon hatası — yalnız 1X2 olayları.</summary>
        public double ResultCalibrationError { get; set; }
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

    /// <summary>Ev sahibi / beraberlik yanlılığının nedenini ayıran ölçümler (test penceresi).</summary>
    public sealed class BiasAudit
    {
        public int Matches { get; set; }
        public double ActualHomeRate { get; set; }
        public double ActualDrawRate { get; set; }
        public double ActualAwayRate { get; set; }
        public double MeanPredictedHome { get; set; }
        public double MeanPredictedDraw { get; set; }
        public double MeanPredictedAway { get; set; }
        /// <summary>1X2'de en yüksek olasılığın hangi sonuca düştüğü (argmax payları).</summary>
        public double ModelPicksHomeShare { get; set; }
        public double ModelPicksDrawShare { get; set; }
        public double ModelPicksAwayShare { get; set; }
        /// <summary>Beraberlik olasılığı dilimlerinde gerçekleşen beraberlik oranı.</summary>
        public List<ReliabilityBand> DrawCalibration { get; set; } = new();
        /// <summary>Denk güçteki maçlar (|P1 − P2| &lt; 0,10).</summary>
        public int BalancedMatches { get; set; }
        public double BalancedActualDrawRate { get; set; }
        public double BalancedMeanPredictedDraw { get; set; }
        public double BalancedActualHomeRate { get; set; }
        /// <summary>Düşük gol beklentili maçlar (λ_ev + λ_dep &lt; 2,3).</summary>
        public int LowGoalMatches { get; set; }
        public double LowGoalActualDrawRate { get; set; }
        public double LowGoalMeanPredictedDraw { get; set; }
        /// <summary>Beraberliğin 1X2 içinde argmax olabilmesi için gereken en düşük olasılık dilimine düşen maç sayısı.</summary>
        public double MaxPredictedDraw { get; set; }
        /// <summary>Ana sonuç kartında beraberliğin seçim skoru en yüksek olan maç payı (argmax kuralının beraberliği bastırıp bastırmadığı).</summary>
        public double DrawHighestSelectionScoreShare { get; set; }
        public double MainResultCardDrawShare { get; set; }
        /// <summary>Lig tabanında ev/deplasman gol oranının ortalaması (iç saha avantajının tek uygulandığı yer).</summary>
        public double MeanLeagueHomeAwayGoalRatio { get; set; }
        public string HomeAdvantageStructure { get; set; } =
            "İç saha avantajı yalnız lig tabanındaki ev/deplasman gol ortalaması farkıyla BİR KEZ uygulanır; takım reytinginde ayrı iç saha terimi yoktur. " +
            "Form (son 10 maç) olasılığa girmez, yalnız metinde kullanılır; takım gücü ile form aynı etkiyi ikinci kez üretmez.";
    }

    public sealed class CrossLeagueAudit
    {
        public int CurrentModelMatches { get; set; }
        public int ComparedMatches { get; set; }
        public int GatedMatches { get; set; }
        public Dictionary<string, int> GateReasons { get; set; } = new();
        public GroupMetrics? CurrentModel { get; set; }
        public GroupMetrics? PreviousModel { get; set; }
        /// <summary>Aynı maçlarda ev sahibi olasılığı &gt; %65 verilen ve ev sahibinin kaybetmediği/kaybettiği dağılım.</summary>
        public int PreviousModelExtremeHomeCalls { get; set; }
        public int CurrentModelExtremeHomeCalls { get; set; }
        public double PreviousExtremeHomeHitRate { get; set; }
        public double CurrentExtremeHomeHitRate { get; set; }
    }

    public sealed class OutcomeBacktestReport
    {
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string EligibilityPolicyVersion { get; set; } = EligibilityPolicy.Version;
        public DateTime EvalStartUtc { get; set; }
        public DateTime CalibrationStartUtc { get; set; }
        public DateTime TestStartUtc { get; set; }
        public DateTime TestEndUtc { get; set; }
        public int HistoricalMatchesProcessed { get; set; }
        public int CompetitionsClassifiedAsLeague { get; set; }
        public int TrainMatches { get; set; }
        public int CalibrationMatches { get; set; }
        public int TestMatches { get; set; }
        public int InsufficientDataMatches { get; set; }
        public Dictionary<string, double> LearningRateSearch { get; set; } = new();
        public double ChosenLearningRate { get; set; }
        public double ChosenStrengthLearningRate { get; set; }
        public double ChosenSeasonCarry { get; set; }
        public bool CalibrationApplied { get; set; }
        public double CalibrationWindowImprovement { get; set; }
        public ModelVariantMetrics TestRaw { get; set; } = new();
        public ModelVariantMetrics TestCalibrated { get; set; } = new();
        public ModelVariantMetrics TestLeagueBaseline { get; set; } = new();
        /// <summary>Ligler arası ortak ölçekten önceki sürüm (2.0) aynı pencerelerde, aynı seçim prosedürüyle.</summary>
        public ModelVariantMetrics? TestPreviousModel { get; set; }
        /// <summary>Bir önceki ÜRETİM sürümü (3.0 — parsimoni kapısı olmadan argmin seçimi) aynı pencerelerde, AYNI test maçlarında.</summary>
        public ModelVariantMetrics? TestPreviousProduction { get; set; }
        public List<GroupMetrics> PreviousProductionLeagues { get; set; } = new();
        /// <summary>Aday/üretim ayrımı: parametre seçimi parsimoni kapısından mı geçti?</summary>
        public bool ParsimoniousSelection { get; set; }
        /// <summary>4.0 − 3.0 eşli 1X2 log loss farkı ve %95 bootstrap aralığı (negatif = yeni sürüm iyi).</summary>
        public double PreviousProductionLogLossDiff { get; set; }
        public double PreviousProductionLogLossDiffCiLow { get; set; }
        public double PreviousProductionLogLossDiffCiHigh { get; set; }
        public List<LeagueMetric> Leagues { get; set; } = new();
        /// <summary>Lig bazlı bağımsız sınav + uygunluk kararı (Enabled/Limited/Disabled).</summary>
        public List<GroupMetrics> LeagueEligibility { get; set; } = new();
        /// <summary>ORGANİZASYON × MARKET AİLESİ matrisi — yayın kararının birinci katmanı.</summary>
        public List<MarketFamilyMetrics> MarketEligibility { get; set; } = new();
        public string MarketEligibilityPolicyVersion { get; set; } = MarketEligibilityPolicy.Version;
        public List<GroupMetrics> PreviousModelLeagues { get; set; } = new();
        public BiasAudit Bias { get; set; } = new();
        public BiasAudit? PreviousModelBias { get; set; }
        public CrossLeagueAudit CrossLeague { get; set; } = new();
        public MainCardAudit MainCards { get; set; } = new();
        public LegacyRankingAudit LegacyRanking { get; set; } = new();
        public OutcomeModelParameters Parameters { get; set; } = new();
        public string Decision { get; set; } = string.Empty;
    }

    /// <summary>
    /// ZAMANSAL GERİYE DÖNÜK TEST — maçlar başlama saatine göre sırayla işlenir: her maçın tahmini yalnız ondan ÖNCE bitmiş
    /// maçlardan kurulmuş reytingle yapılır, sonra maç modele eklenir (sızıntı yok). Pencereler:
    ///   eğitim [evalStart, calStart) → öğrenme oranları + sezon daraltması seçimi;
    ///   kalibrasyon [calStart, testStart) → kalibrasyon parametreleri + güvenlik sınırları seçimi;
    ///   test [testStart, testEnd) → YALNIZ raporlama ve lig uygunluk kararı (test penceresine bakılarak parametre seçilmez).
    /// Lig ortalaması tabanı da sızıntısızdır: her maçta o organizasyonun o ana kadarki sonuç frekanslarıdır.
    /// </summary>
    public static class OutcomeBacktest
    {
        public static readonly double[] LearningRates = { 0.03, 0.05, 0.07 };
        public static readonly double[] StrengthLearningRates = { 0.0, 0.02 };
        public static readonly double[] CrossLeagueTeamWeights = { 1.0, 0.5, 0.25 };
        public static readonly double[] SeasonCarries = { 1.0, 0.8, 0.6 };
        private static readonly double[] GoalScales = { 0.92, 0.96, 1.0, 1.04, 1.08 };
        private static readonly double[] DrawInflations = { 0.95, 1.0, 1.08, 1.16, 1.25 };
        private static readonly double[] BaselineMixes = { 0.0, 0.05, 0.12, 0.2 };
        private static readonly double[] UncertaintyMixes = { 0.0, 0.2, 0.4, 0.6 };
        private static readonly double[] TotalGoalShrinks = { 1.0, 0.75, 0.5, 0.25, 0.0 };
        /// <summary>Aday 4.0 — veri azaldıkça yüzdelerin lig tabanına çekilme oranının alt sınırı (ürün kuralı, ölçüm değil).</summary>
        public const double MinUncertaintyMix = 0.2;
        public const double LeagueShrinkageK = 200;
        public const double MinimumCalibrationGain = 0.0005;

        /// <summary>
        /// LİG BAZLI BERABERLİK/İÇ SAHA KALİBRASYONU — aday katman (5.0 araştırması, 19.09.2026).
        ///
        /// TEŞHİS: kapalı 7 organizasyonun 6'sında 1X2 modeli tabanı ANLAMLI geçiyor (CI üst ucu &lt; 0)
        /// ama hücre CALIBRATION_ERROR ve SEGMENT_BIAS ile kapanıyor. 4.0'da lig başına öğrenilen
        /// TEK kalibrasyon parametresi <see cref="OutcomeModelParameters.LeagueGoalScale"/>'dir;
        /// <see cref="OutcomeModelParameters.LeagueDrawInflation"/> ve
        /// <see cref="OutcomeModelParameters.LeagueHomeTilt"/> tanımlı ve okunuyor ama HİÇBİR YERDE
        /// DOLDURULMUYOR. Yani modelin lig başına toplam gol ayarı var, beraberlik ve iç saha ayarı YOK —
        /// tam da kapanmaya yol açan iki eksen.
        ///
        /// Bu bayrak açıkken ikisi de kalibrasyon penceresinde lig başına aranır ve global değere
        /// aynı <see cref="LeagueShrinkageK"/> ile daraltılır. Eşikler DEĞİŞMEZ.
        /// </summary>
        private static readonly double[] HomeTilts = { -0.10, -0.05, -0.025, 0.0, 0.025, 0.05, 0.10 };

        /// <summary>
        /// Dixon–Coles düşük skor düzeltmesi ρ için arama ızgarası. 0 nötrdür (bağımsız Poisson).
        /// Aynı sözleşme: <see cref="OutcomeModelParameters.LeagueLowScoreRho"/> tanımlı ve okunuyordu
        /// ama HİÇBİR YERDE doldurulmuyordu. Ölçüldü (25.09.2026): basit Dixon–Coles referansı Serie A'da
        /// Base 4.0'ı geçiyor (0,98692 &lt; 0,98999) — bu eksenin lig başına aranması gerekçelidir.
        /// </summary>
        private static readonly double[] LowScoreRhos = { -0.15, -0.10, -0.05, 0.0, 0.05 };

        /// <summary>Lig bazlı kalibrasyon adayının hangi eksenleri açtığı (ablasyon için ayrı ayrı kapatılabilir).</summary>
        [Flags]
        public enum LeagueCalibrationAxes
        {
            None = 0,
            /// <summary>Lig başına beraberlik şişirmesi.</summary>
            Draw = 1,
            /// <summary>Lig başına iç saha eğimi.</summary>
            HomeTilt = 2,
            /// <summary>Lig başına Dixon–Coles düşük skor düzeltmesi.</summary>
            LowScoreRho = 4,
            All = Draw | HomeTilt | LowScoreRho
        }
        public const double BaselinePriorWeight = 20;

        /// <summary>Backtest yöntem sürümü — "kickoff-batch": eşzamanlı maçlar birbirinin sonucunu göremez.</summary>
        public const string MethodVersion = "kickoff-batch-1";

        private sealed record Collected(List<EvalSample> Samples, Dictionary<int, int> NotPredictedByLeague, Dictionary<string, int> GateReasons);

        /// <summary>Eski imza (tek lig/sınıflamasız): ligler arası katman kapalı.</summary>
        public static OutcomeBacktestReport Run(IReadOnlyList<HistoricalMatch> ordered, ISet<int> evalLeagues,
            DateTime evalStart, DateTime calStart, DateTime testStart, DateTime testEnd)
            => Run(ordered, CompetitionCatalog.Unclassified, evalLeagues, evalStart, calStart, testStart, testEnd, testEnd, compareLegacy: false);

        public static OutcomeBacktestReport Run(IReadOnlyList<HistoricalMatch> ordered, CompetitionCatalog catalog, ISet<int> evalLeagues,
            DateTime evalStart, DateTime calStart, DateTime testStart, DateTime testEnd, DateTime nowUtc, bool compareLegacy = true,
            bool candidate = false, LeagueCalibrationAxes leagueCalibration = LeagueCalibrationAxes.None)
        {
            var report = new OutcomeBacktestReport
            {
                EvalStartUtc = evalStart, CalibrationStartUtc = calStart, TestStartUtc = testStart, TestEndUtc = testEnd,
                HistoricalMatchesProcessed = ordered.Count, CompetitionsClassifiedAsLeague = catalog.LeagueCount
            };
            // Değerlendirme kümesi: kilitli ligler + (sınıflama varsa) ligler arası maçlar — modelden bağımsız, sınıflamadan.
            bool InEval(HistoricalMatch m) => evalLeagues.Contains(m.LeagueId);
            bool InCross(HistoricalMatch m) => catalog.LeagueCount > 0 && !catalog.IsLeague(m.LeagueId) && !catalog.IsExcluded(m.LeagueId);

            var space = catalog.LeagueCount > 0
                ? (from lr in LearningRates from c in SeasonCarries select (lr, 0.02, c)).ToList()
                : LearningRates.Select(lr => (lr, 0.0, 1.0)).ToList();
            var current = Fit(ordered, catalog, InEval, InCross, space, evalStart, calStart, testStart, testEnd, crossAware: catalog.LeagueCount > 0, report, candidate, leagueCalibration);

            report.Parameters = current.Chosen;
            report.ChosenLearningRate = current.Chosen.LearningRate;
            report.ChosenStrengthLearningRate = current.Chosen.StrengthLearningRate;
            report.ChosenSeasonCarry = current.Chosen.SeasonCarry;
            report.TrainMatches = current.TrainCount;
            report.CalibrationMatches = current.Cal.Count;
            report.CalibrationApplied = current.CalibrationApplied;
            report.CalibrationWindowImprovement = current.CalibrationImprovement;

            var testLocked = current.Test.Samples.Where(s => evalLeagues.Contains(s.LeagueId)).ToList();
            report.TestMatches = testLocked.Count;
            report.InsufficientDataMatches = current.Test.NotPredictedByLeague.Where(k => evalLeagues.Contains(k.Key)).Sum(k => k.Value);

            var chosen = current.Chosen;
            var baseParams = current.BaseParams;
            report.TestRaw = Evaluate("Raw", testLocked, s => OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Raw);
            report.TestCalibrated = Evaluate("Calibrated", testLocked, s => OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated);
            report.TestLeagueBaseline = EvaluateEmpirical("LeagueAverageBaseline", testLocked);
            report.Leagues = testLocked.GroupBy(s => s.LeagueId).Select(g => new LeagueMetric
            {
                LeagueId = g.Key,
                Matches = g.Count(),
                ResultLogLossCalibrated = Math.Round(g.Average(s => ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated, s)), 4),
                ResultLogLossRaw = Math.Round(g.Average(s => ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Raw, s)), 4),
                ResultLogLossLeagueBaseline = Math.Round(g.Average(s => GroupEvaluator.ResultLoss(s.BaseHome, s.BaseDraw, s.BaseAway, s.HomeGoals, s.AwayGoals)), 4),
                GoalScale = chosen.LeagueGoalScale.TryGetValue(g.Key, out var sc) ? sc : chosen.GoalScale
            }).OrderBy(l => l.LeagueId).ToList();

            var recentFrom = nowUtc.AddDays(-60);
            var recentByLeague = ordered.Where(m => m.KickoffUtc >= recentFrom && m.KickoffUtc < nowUtc).GroupBy(m => m.LeagueId).ToDictionary(g => g.Key, g => g.Count());
            report.LeagueEligibility = LeagueGroups(evalLeagues, current.Test, chosen, recentByLeague);
            report.MarketEligibility = MarketGroups(evalLeagues, current.Test, chosen, recentByLeague);
            report.Bias = AuditBias(testLocked, chosen);
            report.MainCards = AuditMainCards(testLocked, chosen);
            report.LegacyRanking = AuditLegacy(testLocked, baseParams);

            // ── Önceki sürüm (2.0): aynı pencere, aynı seçim prosedürü, ligler arası katman kapalı ──
            FitResult? legacy = null;
            if (compareLegacy)
            {
                var legacySpace = LearningRates.Select(lr => (lr, 0.0, 1.0)).ToList();
                legacy = Fit(ordered, CompetitionCatalog.Unclassified, InEval, InCross, legacySpace, evalStart, calStart, testStart, testEnd, crossAware: false, null);
                var lp = legacy.Chosen;
                var legacyLocked = legacy.Test.Samples.Where(s => evalLeagues.Contains(s.LeagueId)).ToList();
                report.TestPreviousModel = Evaluate("PreviousModel_" + OutcomeModelVersion.NoCrossScale, legacyLocked, s => OutcomePredictor.Predict(s.E, s.LeagueId, lp).Calibrated);
                report.PreviousModelLeagues = LeagueGroups(evalLeagues, legacy.Test, lp, recentByLeague);
                report.PreviousModelBias = AuditBias(legacyLocked, lp);
            }
            report.CrossLeague = AuditCrossLeague(current, legacy, catalog);

            // ── Bir önceki ÜRETİM sürümü (3.0): aynı pencere, aynı reyting arama uzayı, parsimoni kapısı KAPALI ──
            report.ParsimoniousSelection = candidate;
            if (candidate)
            {
                var prod = Fit(ordered, catalog, InEval, InCross, space, evalStart, calStart, testStart, testEnd, crossAware: catalog.LeagueCount > 0, null, candidate: false);
                var prodById = prod.Test.Samples.Where(s => evalLeagues.Contains(s.LeagueId)).ToDictionary(s => s.MatchId);
                var paired = testLocked.Where(s => prodById.ContainsKey(s.MatchId)).ToList();
                report.TestPreviousProduction = Evaluate("PreviousProduction_" + OutcomeModelVersion.Previous,
                    paired.Select(s => prodById[s.MatchId]).ToList(), s => OutcomePredictor.Predict(s.E, s.LeagueId, prod.Chosen).Calibrated);
                report.PreviousProductionLeagues = LeagueGroups(evalLeagues, prod.Test, prod.Chosen, recentByLeague);
                var diffs = paired.Select(s =>
                    ResultLoss(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated, s)
                    - ResultLoss(OutcomePredictor.Predict(prodById[s.MatchId].E, s.LeagueId, prod.Chosen).Calibrated, s)).ToArray();
                if (diffs.Length > 0)
                {
                    report.PreviousProductionLogLossDiff = Math.Round(diffs.Average(), 5);
                    var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, seed: 909);
                    report.PreviousProductionLogLossDiffCiLow = Math.Round(lo, 5);
                    report.PreviousProductionLogLossDiffCiHigh = Math.Round(hi, 5);
                }
            }

            report.Decision = report.CalibrationApplied
                ? (report.TestCalibrated.CombinedLogLoss <= report.TestRaw.CombinedLogLoss ? "CALIBRATION_APPLIED_TEST_IMPROVED" : "CALIBRATION_APPLIED_TEST_NOT_IMPROVED")
                : "RAW_MODEL_KEPT_NO_CALIBRATION_GAIN";
            LastArtifacts = new BacktestArtifacts(report, testLocked, current.Test.Samples, current.Cal, chosen, baseParams, current.PreTest);
            return report;
        }

        /// <summary>
        /// Koşunun maç düzeyi çıktıları — YALNIZ çevrimdışı denetim (segment tabloları, gerçek maç sanity kontrolü, dış oran
        /// karşılaştırması) içindir; üretim yolu bu alanı okumaz ve DB'ye yazılmaz.
        /// </summary>
        public sealed record BacktestArtifacts(OutcomeBacktestReport Report, IReadOnlyList<EvalSample> LockedTestSamples,
            IReadOnlyList<EvalSample> AllTestSamples, IReadOnlyList<EvalSample> CalibrationSamples,
            OutcomeModelParameters Chosen, OutcomeModelParameters BaseParams, IReadOnlyList<EvalSample>? PreTestSamples = null);

        /// <summary>Son <see cref="Run"/> çağrısının maç düzeyi çıktıları (tek iş parçacıklı çevrimdışı kullanım).</summary>
        [ThreadStatic] public static BacktestArtifacts? LastArtifacts;

        private sealed record FitResult(OutcomeModelParameters Chosen, OutcomeModelParameters BaseParams, List<EvalSample> Cal, Collected Test,
            int TrainCount, bool CalibrationApplied, double CalibrationImprovement, List<EvalSample>? PreTest = null);

        private static FitResult Fit(IReadOnlyList<HistoricalMatch> ordered, CompetitionCatalog catalog, Func<HistoricalMatch, bool> inEval,
            Func<HistoricalMatch, bool> inCross, List<(double Lr, double Slr, double Carry)> space,
            DateTime evalStart, DateTime calStart, DateTime testStart, DateTime testEnd, bool crossAware, OutcomeBacktestReport? report,
            bool candidate = false, LeagueCalibrationAxes leagueCalibration = LeagueCalibrationAxes.None)
        {
            var identity = new OutcomeModelParameters { GoalScale = 1, DrawInflation = 1, BaselineMix = 0, UncertaintyMix = 0, CrossLeagueAware = crossAware };

            // 1) Reyting parametreleri — eğitim penceresinde ham (kalibrasyonsuz) birleşik log loss.
            double bestLoss = double.MaxValue; var best = space[0]; var trainCount = 0;
            foreach (var cand in candidate ? space.Where(x => x.Carry >= 1).ToList() : space)
            {
                var p = identity.Clone(); p.LearningRate = cand.Lr; p.StrengthLearningRate = cand.Slr; p.SeasonCarry = cand.Carry;
                // Reyting parametreleri geniş veriyle seçilir (eğitim penceresindeki bütün rekabetçi maçlar): kilitli liglerin
                // eğitim penceresi tek başına ~200 maç (ölçüm 17.09.2026) — seçim gürültüye dayanırdı.
                var c = Collect(ordered, catalog, p, evalStart, calStart, m => !catalog.IsExcluded(m.LeagueId));
                var loss = c.Samples.Count == 0 ? double.MaxValue : c.Samples.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                if (report != null)
                    report.LearningRateSearch[string.Create(CultureInfo.InvariantCulture, $"lr={cand.Lr:0.00};s={cand.Slr:0.00};carry={cand.Carry:0.0}")] = Math.Round(loss, 5);
                if (loss < bestLoss) { bestLoss = loss; best = cand; trainCount = c.Samples.Count; }
            }

            var baseParams = identity.Clone(); baseParams.LearningRate = best.Lr; baseParams.StrengthLearningRate = best.Slr; baseParams.SeasonCarry = best.Carry;

            // 1a-parsimoni (aday 4.0) — SEZON DARALTMASI yalnız KANITLANIRSA açılır.
            // Ölçüm 18.09.2026 (seçim penceresi, 4.356 maç): carry 0,8 ↔ 1,0 farkı −0,0008 ve %95 eşli aralık [−0,0029, +0,0013];
            // yani seçim ölçütü bu parametreyi ayırt EDEMİYOR. 3.0 yine de argmin'i (0,8) alıyordu. Aday: ayırt edilemiyorsa
            // nötr değerde (daraltma yok) kalınır.
            if (candidate)
            {
                foreach (var carry in SeasonCarries.Where(c => c < 1))
                {
                    var p = baseParams.Clone(); p.SeasonCarry = carry;
                    var (mean, _, hi) = PairedSelection(ordered, catalog, baseParams, p, evalStart, calStart, m => !catalog.IsExcluded(m.LeagueId));
                    report?.LearningRateSearch.TryAdd(string.Create(CultureInfo.InvariantCulture, $"parsimony:carry={carry:0.0};mean={mean:0.00000};ciHigh={hi:0.00000}"), Math.Round(hi, 5));
                    if (hi < 0) { baseParams.SeasonCarry = carry; break; }
                }
            }

            // 1b) Ligler arası parametreler (lig ofseti öğrenme oranı × takım payı) YALNIZ ligler arası maçlarda, testten ÖNCEKİ
            //     pencerede seçilir: lig içi maçta ofset sadeleşir, genel kayıp bu parametreyi ölçemez (ölçüm 17.09.2026: genel
            //     kayıpla seçilen 0,01 oranı Kıbrıs–La Liga farkını 0,33'te bıraktı, Omonia–Celta %71 ev sahibi kaldı).
            if (crossAware && !candidate)
            {
                double bestCross = double.MaxValue;
                foreach (var slr in StrengthLearningRates)
                foreach (var w in CrossLeagueTeamWeights)
                {
                    var p = baseParams.Clone(); p.StrengthLearningRate = slr; p.CrossLeagueTeamWeight = w;
                    var c = Collect(ordered, catalog, p, evalStart, testStart, inCross);
                    var crossSamples = c.Samples.Where(x => x.E.CrossLeague).ToList();
                    var loss = crossSamples.Count == 0 ? double.MaxValue : crossSamples.Average(x => Combined(OutcomePredictor.Predict(x.E, x.LeagueId, p).Calibrated, x));
                    if (report != null)
                        report.LearningRateSearch[string.Create(CultureInfo.InvariantCulture, $"cross:s={slr:0.00};teamWeight={w:0.00};n={crossSamples.Count}")] = Math.Round(loss, 5);
                    if (loss < bestCross) { bestCross = loss; baseParams.StrengthLearningRate = slr; baseParams.CrossLeagueTeamWeight = w; }
                }
            }
            // 1b-parsimoni (aday 4.0) — LİGLER ARASI parametreler.
            // Ölçüm 18.09.2026: seçim penceresinde yalnız 50 ligler arası maç var (lig bağlantısı 20 maçla açıldığı için erken
            // maçlar kapıda eleniyor). 3.0 bu 50 maçta iki parametreyi birden argmin ile seçiyor; takım payının %95 eşli aralığı
            // [−0,045, +0,028] — yani ölçüm parametreyi AYIRT EDEMİYOR ve 3.0 gürültüyü takip ediyor.
            // Nötr değer yapısaldır: Update'te hata zaten lig ofseti DÜŞÜLDÜKTEN sonra hesaplanıyor (çift sayım yok), lig gücü
            // ayrıca bütün ligler arası maç grafiğinden toplu çözülüyor. Dolayısıyla ligler arası maçın takım reytingine katkısı
            // kanıt olmadan kısılmaz: takım payı = 1, çevrimiçi lig ofseti nudge'ı = 0 (toplu çözüm zaten yapıyor).
            if (crossAware && candidate)
            {
                baseParams.CrossLeagueTeamWeight = 1.0;
                baseParams.StrengthLearningRate = 0.0;
                var neutral = baseParams.Clone();
                foreach (var slr in StrengthLearningRates.Where(x => x > 0))
                foreach (var w in CrossLeagueTeamWeights.Where(x => x < 1))
                {
                    var p = neutral.Clone(); p.StrengthLearningRate = slr; p.CrossLeagueTeamWeight = w;
                    var (mean, _, hi) = PairedSelection(ordered, catalog, neutral, p, evalStart, testStart, inCross, crossOnly: true);
                    report?.LearningRateSearch.TryAdd(string.Create(CultureInfo.InvariantCulture, $"parsimony:cross s={slr:0.00};w={w:0.00};mean={mean:0.00000}"), Math.Round(hi, 5));
                    if (hi < 0) { baseParams.StrengthLearningRate = slr; baseParams.CrossLeagueTeamWeight = w; }
                }
            }

            // 2) Seçilen parametrelerle kalibrasyon + test örnekleri (tek geçiş).
            var all = Collect(ordered, catalog, baseParams, evalStart, testEnd, m => inEval(m) || inCross(m));
            var preTest = all.Samples.Where(s => s.KickoffUtc < testStart).ToList();
            var cal = preTest.Where(s => s.KickoffUtc >= calStart).ToList();
            var test = new Collected(all.Samples.Where(s => s.KickoffUtc >= testStart).ToList(),
                all.NotPredictedByLeague, all.GateReasons);
            // Test penceresi dışı (kalibrasyon penceresi) tahmin edilemeyenler sayıma girmesin.
            var testNotPredicted = CountNotPredicted(ordered, catalog, baseParams, calStart, testStart, testEnd, m => inEval(m) || inCross(m));
            test = test with { NotPredictedByLeague = testNotPredicted.ByLeague, GateReasons = testNotPredicted.Reasons };

            // 3) Kalibrasyon — yalnız kalibrasyon penceresi.
            var chosen = baseParams.Clone();
            var applied = false; double improvement = 0;
            if (cal.Count >= 200)
            {
                var identityLoss = cal.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, baseParams).Calibrated, s));
                double bestCal = double.MaxValue;
                OutcomeModelParameters? bestP = null;
                // Koordinat araması (iki tur): (gol ölçeği × beraberlik × toplam gol daraltması) → (sabit karışım × belirsizlik karışımı).
                // Aday 4.0'da belirsizlik karışımı ürün kuralı gereği tabandan (MinUncertaintyMix) başlar ve altına inemez.
                var cur = baseParams.Clone(); cur.UncertaintyMix = candidate ? MinUncertaintyMix : 0; cur.BaselineMix = 0;
                for (var round = 0; round < 2; round++)
                {
                    foreach (var gs in GoalScales)
                    foreach (var d in DrawInflations)
                    foreach (var g in TotalGoalShrinks)
                    {
                        var p = cur.Clone(); p.GoalScale = gs; p.DrawInflation = d; p.TotalGoalShrink = g;
                        var loss = cal.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                        if (loss < bestCal) { bestCal = loss; bestP = p; }
                    }
                    cur = (bestP ?? cur).Clone();
                    // Aday 4.0 — BELİRSİZLİK TABANI: kullanıcıya "sınırlı veri: yüzdeler lig ortalamasına yaklaştırıldı" yazılıyordu
                    // ama 3.0'da kalibrasyon araması belirsizlik karışımını 0 seçtiği için HİÇBİR daraltma yapılmıyordu (ölçüm
                    // 18.09.2026: SV Elversberg–Bayern, kapsam %33, belirsizlik ağırlığı %0). Belirsizlik bir etiket değil, ürün
                    // kuralıdır: veri azaldıkça yüzdeler lig tabanına çekilir. Ölçülen bedel test penceresinde +0,0001 birleşik
                    // log loss; 1X2 log loss değişmiyor, 1X2 ECE 0,0039 → 0,0036 iyileşiyor.
                    foreach (var bm in BaselineMixes)
                    foreach (var um in (candidate ? UncertaintyMixes.Where(x => x >= MinUncertaintyMix) : UncertaintyMixes))
                    {
                        var p = cur.Clone(); p.BaselineMix = bm; p.UncertaintyMix = um;
                        var loss = cal.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                        if (loss < bestCal) { bestCal = loss; bestP = p; }
                    }
                    cur = (bestP ?? cur).Clone();
                }
                improvement = Math.Round(identityLoss - bestCal, 5);
                if (bestP != null && identityLoss - bestCal >= MinimumCalibrationGain)
                {
                    chosen = bestP;
                    applied = true;
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

                        // ── ADAY: lig bazlı BERABERLİK ve İÇ SAHA kalibrasyonu ────────────────
                        // Arama koordinat sırasıyla yapılır (beraberlik → iç saha) ve her biri global
                        // değere aynı daraltmayla çekilir. Kalibrasyon penceresi dışına BAKILMAZ.
                        if (leagueCalibration == LeagueCalibrationAxes.None) continue;

                        if (leagueCalibration.HasFlag(LeagueCalibrationAxes.Draw))
                        {
                            double db = double.MaxValue, lDraw = chosen.DrawInflation;
                            foreach (var d in DrawInflations)
                            {
                                var p = chosen.Clone(); p.LeagueDrawInflation.Clear(); p.LeagueDrawInflation[g.Key] = d;
                                var loss = g.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                                if (loss < db) { db = loss; lDraw = d; }
                            }
                            chosen.LeagueDrawInflation[g.Key] =
                                Math.Round((n * lDraw + LeagueShrinkageK * chosen.DrawInflation) / (n + LeagueShrinkageK), 4);
                        }

                        if (leagueCalibration.HasFlag(LeagueCalibrationAxes.HomeTilt))
                        {
                            double tb = double.MaxValue, lTilt = chosen.HomeTilt;
                            foreach (var t in HomeTilts)
                            {
                                var p = chosen.Clone(); p.LeagueHomeTilt.Clear(); p.LeagueHomeTilt[g.Key] = t;
                                var loss = g.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                                if (loss < tb) { tb = loss; lTilt = t; }
                            }
                            chosen.LeagueHomeTilt[g.Key] =
                                Math.Round((n * lTilt + LeagueShrinkageK * chosen.HomeTilt) / (n + LeagueShrinkageK), 4);
                        }

                        if (leagueCalibration.HasFlag(LeagueCalibrationAxes.LowScoreRho))
                        {
                            double rb = double.MaxValue, lRho = chosen.LowScoreRho;
                            foreach (var rho in LowScoreRhos)
                            {
                                var p = chosen.Clone(); p.LeagueLowScoreRho.Clear(); p.LeagueLowScoreRho[g.Key] = rho;
                                var loss = g.Average(s => Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s));
                                if (loss < rb) { rb = loss; lRho = rho; }
                            }
                            chosen.LeagueLowScoreRho[g.Key] =
                                Math.Round((n * lRho + LeagueShrinkageK * chosen.LowScoreRho) / (n + LeagueShrinkageK), 4);
                        }
                    }
                }
            }
            // Güvenlik sınırları testten ÖNCEKİ bütün örneklerden (eğitim + kalibrasyon) ölçülür.
            DeriveSafetyLimits(chosen, preTest);
            return new FitResult(chosen, baseParams, cal, test, trainCount, applied, improvement, preTest);
        }

        /// <summary>
        /// PARSİMONİ SINAVI — bir parametre nötr değerinden ancak SEÇİM penceresinde AYNI maçlarda ölçülen eşli kayıp farkının
        /// %95 bootstrap aralığı tamamen 0'ın altındaysa ayrılır. Aynı maç iki parametreyle iki kez tahmin edilir; fark maç
        /// düzeyinde eşlenir (farklı parametrede kapıya takılan maçlar eşlemeden düşer). Test penceresine BAKILMAZ.
        /// </summary>
        private static (double Mean, double Low, double High) PairedSelection(IReadOnlyList<HistoricalMatch> ordered, CompetitionCatalog catalog,
            OutcomeModelParameters neutral, OutcomeModelParameters cand, DateTime from, DateTime to, Func<HistoricalMatch, bool> include,
            bool crossOnly = false)
        {
            Dictionary<int, double> Losses(OutcomeModelParameters p)
            {
                var c = Collect(ordered, catalog, p, from, to, include);
                var src = crossOnly ? c.Samples.Where(s => s.E.CrossLeague) : c.Samples;
                var d = new Dictionary<int, double>();
                foreach (var s in src) d[s.MatchId] = Combined(OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated, s);
                return d;
            }
            var a = Losses(neutral);
            var b = Losses(cand);
            var diffs = new List<double>(Math.Min(a.Count, b.Count));
            foreach (var kv in b) if (a.TryGetValue(kv.Key, out var v)) diffs.Add(kv.Value - v);
            if (diffs.Count < 30) return (0, double.MinValue, double.MaxValue); // ölçülemeyen parametre nötr kalır
            var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, seed: 4242);
            return (diffs.Average(), lo, hi);
        }

        /// <summary>
        /// Güvenlik sınırları KALİBRASYON penceresinden ölçülür:
        ///  • MaxSupportedProbability: en yüksek 1X2 olasılığı dilimlerinde (0,05 genişlik, ≥ 30 örnek) gerçekleşme oranı tahminin
        ///    0,08 altına düşmeyen son dilimin üst sınırı;
        ///  • EloConflictThreshold: |model beklenen puan − Elo beklentisi| dağılımının 99. yüzdeliği;
        ///  • GoalResidualStd: gol − λ artıklarının standart sapması.
        /// </summary>
        private static void DeriveSafetyLimits(OutcomeModelParameters p, List<EvalSample> cal)
        {
            if (cal.Count < 200) return;
            var tops = new List<(double P, bool Y)>(cal.Count);
            var diffs = new List<double>(cal.Count);
            var resid = new List<double>(cal.Count * 2);
            foreach (var s in cal)
            {
                var pr = OutcomePredictor.Predict(s.E, s.LeagueId, p);
                var d = pr.Calibrated;
                double h = d.HomeWin, x = d.Draw, a = d.AwayWin;
                var top = Math.Max(h, Math.Max(x, a));
                var y = top == h ? s.HomeGoals > s.AwayGoals : top == a ? s.HomeGoals < s.AwayGoals : s.HomeGoals == s.AwayGoals;
                tops.Add((top, y));
                diffs.Add(Math.Abs(h + 0.5 * x - s.E.EloHomeExpectation));
                resid.Add(s.HomeGoals - d.ExpectedHome); resid.Add(s.AwayGoals - d.ExpectedAway);
            }
            double supported = 0.55;
            for (var lo = 0.55; lo < 0.99; lo += 0.05)
            {
                var band = tops.Where(t => t.P >= lo && t.P < lo + 0.05).ToList();
                if (band.Count < 30) break;
                if (band.Average(t => t.Y ? 1.0 : 0.0) < band.Average(t => t.P) - 0.08) break;
                supported = Math.Round(lo + 0.05, 2);
            }
            p.MaxSupportedProbability = supported;
            diffs.Sort();
            p.EloConflictThreshold = Math.Round(Math.Max(0.1, diffs[(int)(0.99 * (diffs.Count - 1))]), 4);
            var mean = resid.Average();
            p.GoalResidualStd = Math.Round(Math.Sqrt(resid.Average(r => (r - mean) * (r - mean))), 4);
        }

        private static List<GroupMetrics> LeagueGroups(ISet<int> evalLeagues, Collected test, OutcomeModelParameters p, Dictionary<int, int> recent)
        {
            var list = new List<GroupMetrics>();
            foreach (var league in evalLeagues.OrderBy(x => x))
            {
                var samples = test.Samples.Where(s => s.LeagueId == league).ToList();
                var g = GroupEvaluator.Evaluate("League:" + league, league, samples, s => OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated,
                    test.NotPredictedByLeague.GetValueOrDefault(league), seed: 1000 + league);
                g.FinishedLast60Days = recent.GetValueOrDefault(league);
                EligibilityPolicy.Decide(g);
                list.Add(g);
            }
            return list;
        }

        /// <summary>
        /// ORGANİZASYON × MARKET AİLESİ MATRİSİ — aynı kilitli test örnekleri, her aile için ayrı sınav. Model yeniden
        /// eğitilmez; yalnız ölçüm ve karar.
        /// </summary>
        private static List<MarketFamilyMetrics> MarketGroups(ISet<int> evalLeagues, Collected test, OutcomeModelParameters p, Dictionary<int, int> recent)
        {
            var list = new List<MarketFamilyMetrics>();
            foreach (var league in evalLeagues.OrderBy(x => x))
            {
                var samples = test.Samples.Where(s => s.LeagueId == league).ToList();
                list.AddRange(MarketFamilyEvaluator.EvaluateAll(league, samples,
                    s => OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated,
                    test.NotPredictedByLeague.GetValueOrDefault(league), recent.GetValueOrDefault(league), seed: 5000 + league));
            }
            return list;
        }

        /// <summary>Zamansal toplayıcı — tahmin maçtan ÖNCE, güncelleme SONRA. Taban frekansları da aynı sırayla güncellenir.</summary>
        private static Collected Collect(IReadOnlyList<HistoricalMatch> ordered, CompetitionCatalog catalog, OutcomeModelParameters p,
            DateTime from, DateTime to, Func<HistoricalMatch, bool> include)
        {
            var model = new OutcomeRatingModel(p, catalog);
            var freq = new Frequencies();
            var list = new List<EvalSample>();
            var notPredicted = new Dictionary<int, int>();
            var reasons = new Dictionary<string, int>();
            // SIZINTI DÜZELTMESİ (26.09.2026): AYNI başlama saatindeki maçlar önce BİRLİKTE tahmin edilir, sonra birlikte modele
            // işlenir. Önceden maçlar tek tek işlendiği için 15:00'teki A maçının sonucu, yine 15:00'te başlayan B maçının lig
            // ortalamasına ve taban frekansına giriyordu — B başladığında A'nın sonucu bilinmiyordu (feature zamanı < başlama değil).
            var i = 0;
            while (i < ordered.Count && ordered[i].KickoffUtc < to)
            {
                var j = i;
                while (j < ordered.Count && ordered[j].KickoffUtc == ordered[i].KickoffUtc) j++;
                for (var k = i; k < j; k++)
                {
                    var m = ordered[k];
                    if (m.KickoffUtc < from || !include(m)) continue;
                    var e = model.Expect(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);
                    if (e.Sufficient)
                    {
                        var (bh, bd, ba, bo, bb, b15, b35) = freq.Baseline(m.LeagueId);
                        list.Add(new EvalSample(m.MatchId, m.LeagueId, m.KickoffUtc, e, m.HomeGoals, m.AwayGoals, bh, bd, ba, bo, bb, b15, b35));
                    }
                    else
                    {
                        notPredicted[m.LeagueId] = notPredicted.GetValueOrDefault(m.LeagueId) + 1;
                        foreach (var r in e.GateReasons) reasons[r] = reasons.GetValueOrDefault(r) + 1;
                    }
                }
                for (var k = i; k < j; k++)
                {
                    model.Update(ordered[k]);
                    freq.Add(ordered[k]);
                }
                i = j;
            }
            return new Collected(list, notPredicted, reasons);
        }

        private static (Dictionary<int, int> ByLeague, Dictionary<string, int> Reasons) CountNotPredicted(IReadOnlyList<HistoricalMatch> ordered,
            CompetitionCatalog catalog, OutcomeModelParameters p, DateTime replayFrom, DateTime from, DateTime to, Func<HistoricalMatch, bool> include)
        {
            var c = Collect(ordered, catalog, p, from, to, include);
            return (c.NotPredictedByLeague, c.GateReasons);
        }

        /// <summary>Organizasyon bazlı sızıntısız sonuç frekansları (lig ortalaması tabanı).</summary>
        private sealed class Frequencies
        {
            private const int Slots = 8;
            private readonly Dictionary<int, double[]> _c = new();
            private readonly double[] _g = new double[Slots];

            public void Add(HistoricalMatch m)
            {
                var row = _c.TryGetValue(m.LeagueId, out var r) ? r : _c[m.LeagueId] = new double[Slots];
                foreach (var t in new[] { row, _g })
                {
                    t[0]++;
                    if (m.HomeGoals > m.AwayGoals) t[1]++; else if (m.HomeGoals == m.AwayGoals) t[2]++; else t[3]++;
                    if (m.HomeGoals + m.AwayGoals > 2) t[4]++;
                    if (m.HomeGoals > 0 && m.AwayGoals > 0) t[5]++;
                    if (m.HomeGoals + m.AwayGoals > 1) t[6]++;
                    if (m.HomeGoals + m.AwayGoals > 3) t[7]++;
                }
            }

            /// <summary>Her gol çizgisinin KENDİ sızıntısız lig frekansı — 2.5'in tabanı 1.5 ve 3.5 için kullanılmaz.</summary>
            public (double H, double D, double A, double O25, double Btts, double O15, double O35) Baseline(int leagueId)
            {
                var n = _g[0];
                double G(int i, double fallback) => n > 0 ? _g[i] / n : fallback;
                var gh = G(1, 0.45); var gd = G(2, 0.26); var ga = G(3, 0.29); var go = G(4, 0.5); var gb = G(5, 0.5);
                var g15 = G(6, 0.75); var g35 = G(7, 0.3);
                var row = _c.GetValueOrDefault(leagueId) ?? new double[Slots];
                double S(int i, double prior) => (row[i] + BaselinePriorWeight * prior) / (row[0] + BaselinePriorWeight);
                return (S(1, gh), S(2, gd), S(3, ga), S(4, go), S(5, gb), S(6, g15), S(7, g35));
            }
        }

        private const double Eps = 1e-6;

        private static double ResultLoss(ScoreDistribution d, EvalSample s)
        {
            var p = s.HomeGoals > s.AwayGoals ? d.HomeWin : s.HomeGoals == s.AwayGoals ? d.Draw : d.AwayWin;
            return -Math.Log(Math.Max(Eps, p));
        }

        private static double BinLoss(double p, bool y) => -Math.Log(Math.Max(Eps, y ? p : 1 - p));

        private static double Combined(ScoreDistribution d, EvalSample s)
            => ResultLoss(d, s) + BinLoss(d.Over(2.5), s.HomeGoals + s.AwayGoals > 2.5) + BinLoss(d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0);

        private static ModelVariantMetrics Evaluate(string name, List<EvalSample> samples, Func<EvalSample, ScoreDistribution> dist)
        {
            var m = new ModelVariantMetrics { Variant = name, Matches = samples.Count };
            if (samples.Count == 0) return m;
            double rl = 0, rb = 0, ra = 0;
            var pooled = new List<(double P, bool Y)>();
            var result = new List<(double P, bool Y)>();
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
                result.Add((d.HomeWin, hw)); result.Add((d.Draw, dr)); result.Add((d.AwayWin, aw));
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
            m.ResultCalibrationError = Math.Round(Ece(result, 10), 5);
            m.Bands = Bands(pooled);
            return m;
        }

        /// <summary>Lig ortalaması tabanı (sızıntısız organizasyon frekansları) — 1X2, 2.5 ve KG.</summary>
        private static ModelVariantMetrics EvaluateEmpirical(string name, List<EvalSample> samples)
        {
            var m = new ModelVariantMetrics { Variant = name, Matches = samples.Count };
            if (samples.Count == 0) return m;
            double rl = 0, rb = 0;
            var result = new List<(double, bool)>();
            var o25 = new List<(double, bool)>(); var bt = new List<(double, bool)>();
            foreach (var s in samples)
            {
                var hw = s.HomeGoals > s.AwayGoals; var dr = s.HomeGoals == s.AwayGoals; var aw = s.HomeGoals < s.AwayGoals;
                rl += GroupEvaluator.ResultLoss(s.BaseHome, s.BaseDraw, s.BaseAway, s.HomeGoals, s.AwayGoals);
                rb += Sq(s.BaseHome - (hw ? 1 : 0)) + Sq(s.BaseDraw - (dr ? 1 : 0)) + Sq(s.BaseAway - (aw ? 1 : 0));
                result.Add((s.BaseHome, hw)); result.Add((s.BaseDraw, dr)); result.Add((s.BaseAway, aw));
                o25.Add((s.BaseOver25, s.HomeGoals + s.AwayGoals > 2)); bt.Add((s.BaseBtts, s.HomeGoals > 0 && s.AwayGoals > 0));
            }
            var n = samples.Count;
            m.ResultLogLoss = Math.Round(rl / n, 5);
            m.ResultBrier = Math.Round(rb / n, 5);
            m.ResultAccuracy = Math.Round(samples.Average(s => (s.BaseHome >= s.BaseDraw && s.BaseHome >= s.BaseAway ? s.HomeGoals > s.AwayGoals
                : s.BaseAway >= s.BaseDraw ? s.HomeGoals < s.AwayGoals : s.HomeGoals == s.AwayGoals) ? 1.0 : 0.0), 4);
            m.Over25 = Bin(o25); m.Btts = Bin(bt);
            m.CombinedLogLoss = Math.Round(m.ResultLogLoss + m.Over25.LogLoss + m.Btts.LogLoss, 5);
            m.ResultCalibrationError = Math.Round(Ece(result, 10), 5);
            m.CalibrationError = m.ResultCalibrationError;
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
            return BandsOf(xs, defs);
        }

        private static List<ReliabilityBand> BandsOf(IReadOnlyList<(double P, bool Y)> xs, (string Name, double Lo, double Hi)[] defs)
            => defs.Select(b =>
            {
                var g = xs.Where(x => x.P >= b.Lo && x.P < b.Hi).ToList();
                return new ReliabilityBand
                {
                    Band = b.Name, Count = g.Count,
                    MeanPredicted = g.Count == 0 ? 0 : Math.Round(g.Average(x => x.P), 4),
                    ObservedFrequency = g.Count == 0 ? 0 : Math.Round(g.Average(x => x.Y ? 1.0 : 0.0), 4)
                };
            }).ToList();

        private static BiasAudit AuditBias(List<EvalSample> test, OutcomeModelParameters p)
        {
            var a = new BiasAudit { Matches = test.Count };
            if (test.Count == 0) return a;
            var rows = test.Select(s =>
            {
                var d = OutcomePredictor.Predict(s.E, s.LeagueId, p).Calibrated;
                return (s, h: d.HomeWin, x: d.Draw, aw: d.AwayWin, lam: s.E.LambdaHome + s.E.LambdaAway);
            }).ToList();
            double n = rows.Count;
            a.ActualHomeRate = R(rows.Count(r => r.s.HomeGoals > r.s.AwayGoals) / n);
            a.ActualDrawRate = R(rows.Count(r => r.s.HomeGoals == r.s.AwayGoals) / n);
            a.ActualAwayRate = R(rows.Count(r => r.s.HomeGoals < r.s.AwayGoals) / n);
            a.MeanPredictedHome = R(rows.Average(r => r.h));
            a.MeanPredictedDraw = R(rows.Average(r => r.x));
            a.MeanPredictedAway = R(rows.Average(r => r.aw));
            a.ModelPicksHomeShare = R(rows.Count(r => r.h >= r.x && r.h >= r.aw) / n);
            a.ModelPicksDrawShare = R(rows.Count(r => r.x > r.h && r.x >= r.aw) / n);
            a.ModelPicksAwayShare = R(rows.Count(r => r.aw > r.h && r.aw > r.x) / n);
            a.MaxPredictedDraw = R(rows.Max(r => r.x));
            a.DrawCalibration = BandsOf(rows.Select(r => (r.x, r.s.HomeGoals == r.s.AwayGoals)).ToList(), new[]
            {
                ("<20", 0.0, 0.20), ("20-24", 0.20, 0.25), ("25-29", 0.25, 0.30), ("30-34", 0.30, 0.35), ("35+", 0.35, 1.01)
            });
            var bal = rows.Where(r => Math.Abs(r.h - r.aw) < 0.10).ToList();
            a.BalancedMatches = bal.Count;
            if (bal.Count > 0)
            {
                a.BalancedActualDrawRate = R(bal.Count(r => r.s.HomeGoals == r.s.AwayGoals) / (double)bal.Count);
                a.BalancedActualHomeRate = R(bal.Count(r => r.s.HomeGoals > r.s.AwayGoals) / (double)bal.Count);
                a.BalancedMeanPredictedDraw = R(bal.Average(r => r.x));
            }
            var low = rows.Where(r => r.lam < 2.3).ToList();
            a.LowGoalMatches = low.Count;
            if (low.Count > 0)
            {
                a.LowGoalActualDrawRate = R(low.Count(r => r.s.HomeGoals == r.s.AwayGoals) / (double)low.Count);
                a.LowGoalMeanPredictedDraw = R(low.Average(r => r.x));
            }
            int drawTop = 0, drawMain = 0;
            foreach (var s in test)
            {
                var snap = OutcomeSnapshotBuilder.Build(0, OutcomePredictor.Predict(s.E, s.LeagueId, p), "Ev", "Dep");
                var res = snap.Families.First(f => f.Family == OutcomeSnapshotBuilder.ResultFamily).Items;
                if (res.OrderByDescending(c => c.SelectionScore).First().MarketKey == Domain.Constants.OddsMarketKeys.MsX) drawTop++;
                // Kart sayısı artık dinamik (0–3): maç sonucu kartı olmayabilir.
                if (snap.MainCards.FirstOrDefault(c => c.Family == OutcomeSnapshotBuilder.ResultFamily)?.MarketKey == Domain.Constants.OddsMarketKeys.MsX) drawMain++;
            }
            a.DrawHighestSelectionScoreShare = R(drawTop / n);
            a.MainResultCardDrawShare = R(drawMain / n);
            a.MeanLeagueHomeAwayGoalRatio = R(test.Average(s => s.E.LeagueHome / Math.Max(0.1, s.E.LeagueAway)));
            return a;
        }

        private static CrossLeagueAudit AuditCrossLeague(FitResult current, FitResult? legacy, CompetitionCatalog catalog)
        {
            var audit = new CrossLeagueAudit();
            var cur = current.Test.Samples.Where(s => s.E.CrossLeague).ToList();
            audit.CurrentModelMatches = cur.Count;
            audit.GateReasons = current.Test.GateReasons.Where(r => r.Key.StartsWith("CROSS_") || r.Key.StartsWith("TEAM_LEAGUE")).ToDictionary(k => k.Key, k => k.Value);
            audit.GatedMatches = audit.GateReasons.Values.Sum();
            if (cur.Count == 0) return audit;
            var cp = current.Chosen;
            if (legacy == null)
            {
                audit.CurrentModel = GroupEvaluator.Evaluate("CrossLeague", null, cur, s => OutcomePredictor.Predict(s.E, s.LeagueId, cp).Calibrated, audit.GatedMatches);
                return audit;
            }
            var lp = legacy.Chosen;
            var legacyById = legacy.Test.Samples.ToDictionary(s => s.MatchId);
            var paired = cur.Where(s => legacyById.ContainsKey(s.MatchId)).ToList();
            audit.ComparedMatches = paired.Count;
            audit.CurrentModel = GroupEvaluator.Evaluate("CrossLeague:" + OutcomeModelVersion.Current, null, paired,
                s => OutcomePredictor.Predict(s.E, s.LeagueId, cp).Calibrated, audit.GatedMatches);
            var legacySamples = paired.Select(s => legacyById[s.MatchId]).ToList();
            audit.PreviousModel = GroupEvaluator.Evaluate("CrossLeague:" + OutcomeModelVersion.NoCrossScale, null, legacySamples,
                s => OutcomePredictor.Predict(s.E, s.LeagueId, lp).Calibrated, 0);
            var curExtreme = paired.Select(s => (s, p: OutcomePredictor.Predict(s.E, s.LeagueId, cp).Calibrated.HomeWin)).Where(x => x.p > 0.65).ToList();
            var legExtreme = legacySamples.Select(s => (s, p: OutcomePredictor.Predict(s.E, s.LeagueId, lp).Calibrated.HomeWin)).Where(x => x.p > 0.65).ToList();
            audit.CurrentModelExtremeHomeCalls = curExtreme.Count;
            audit.PreviousModelExtremeHomeCalls = legExtreme.Count;
            audit.CurrentExtremeHomeHitRate = curExtreme.Count == 0 ? 0 : R(curExtreme.Count(x => x.s.HomeGoals > x.s.AwayGoals) / (double)curExtreme.Count);
            audit.PreviousExtremeHomeHitRate = legExtreme.Count == 0 ? 0 : R(legExtreme.Count(x => x.s.HomeGoals > x.s.AwayGoals) / (double)legExtreme.Count);
            return audit;
        }

        private static double R(double v) => Math.Round(v, 4);

        private static MainCardAudit AuditMainCards(List<EvalSample> test, OutcomeModelParameters p)
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
        /// ilk 3'ü gösteriyordu; aynı dağılımdan kurulup aynı kuralla sıralanır.
        /// </summary>
        private static LegacyRankingAudit AuditLegacy(List<EvalSample> test, OutcomeModelParameters p)
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
            // Bileşik (çifte şans) marketler: ana kartlara girmedikleri için üretimde hiç sorulmuyordu; eksik oldukları için
            // sorulduklarında SESSİZCE "tutmadı" sayılıyorlardı — seçim kuralı denetimini yanlış yönde bozan boşluk.
            Domain.Constants.OddsMarketKeys.DoubleChance1X => h >= a,
            Domain.Constants.OddsMarketKeys.DoubleChanceX2 => h <= a,
            Domain.Constants.OddsMarketKeys.DoubleChance12 => h != a,
            _ => false
        };
    }
}
