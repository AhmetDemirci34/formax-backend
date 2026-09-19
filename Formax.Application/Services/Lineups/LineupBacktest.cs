using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;

namespace Formax.Application.Services.Lineups
{
    /// <summary>Tek ablasyonun (ya da aday modelin) ölçüm sonucu.</summary>
    public sealed class LineupAblationResult
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>Kadro düzeltmesi GERÇEKTEN uygulanan maç sayısı.</summary>
        public int AdjustedMatches { get; set; }
        /// <summary>Uygulanan düzeltmelerin ortalama mutlak λ log deltası.</summary>
        public double MeanAbsDelta { get; set; }
        public double MaxAbsDelta { get; set; }
        /// <summary>Bütün test maçları üzerinde (kadrolu + kadrosuz) aday model metrikleri.</summary>
        public List<MarketFamilyMetrics> Overall { get; set; } = new();
        /// <summary>Organizasyon × market ailesi metrikleri.</summary>
        public List<MarketFamilyMetrics> ByLeague { get; set; } = new();
        /// <summary>Aday − taban log loss farkı (yalnız DÜZELTİLMİŞ maçlarda; pozitif = aday kötü).</summary>
        public Dictionary<string, double> AdjustedOnlyLogLossDiff { get; set; } = new();
        public Dictionary<string, double> AdjustedOnlyCiLow { get; set; } = new();
        public Dictionary<string, double> AdjustedOnlyCiHigh { get; set; } = new();
    }

    /// <summary>Kadro/oyuncu katmanının bütün ölçüm raporu.</summary>
    public sealed class LineupBacktestReport
    {
        public string ImpactVersion { get; set; } = LineupImpactVersion.Current;
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime TrainStartUtc { get; set; }
        public DateTime TestStartUtc { get; set; }
        public DateTime NowUtc { get; set; }

        public int HistoryMatches { get; set; }
        public int LineupObservations { get; set; }
        /// <summary>Doğrulama kuralını (C) geçen kadro gözlemi sayısı.</summary>
        public int VerifiedObservations { get; set; }
        public int TestMatches { get; set; }
        /// <summary>Test penceresinde doğrulanmış kadrosu olan maç sayısı.</summary>
        public int TestMatchesWithLineup { get; set; }
        public double LineupCoverage { get; set; }
        /// <summary>Yeterli örnekleme ulaşmış (etkisi gerçekten öğrenilmiş) oyuncu sayısı.</summary>
        public int SufficientPlayers { get; set; }
        public int ResolvedPlayers { get; set; }
        /// <summary>Oyuncu başına başlangıç sayısı dağılımı (n → oyuncu).</summary>
        public Dictionary<int, int> StartsPerPlayer { get; set; } = new();

        /// <summary>Taban (kadrosuz) model metrikleri — ablasyonlarla AYNI maç kümesinde.</summary>
        public List<MarketFamilyMetrics> BaseOverall { get; set; } = new();
        public List<MarketFamilyMetrics> BaseByLeague { get; set; } = new();

        public List<LineupAblationResult> Ablations { get; set; } = new();

        /// <summary>
        /// true ise bu ÖLÇÜM kaynak damgası aranmadan yapıldı (yalnız 22 benzersiz başlangıç kuralı).
        /// Duyarlılık ölçümüdür; üretim kararına TEK BAŞINA temel olamaz.
        /// </summary>
        public bool ProvenanceRelaxed { get; set; }

        /// <summary>Kabul kapısının kararı ve ölçülebilir gerekçeleri.</summary>
        public string Decision { get; set; } = LineupImpactDecisions.Shadow;
        public List<string> DecisionReasons { get; set; } = new();
        /// <summary>Katmanın ÜRETİME açtığı organizasyon × market hücreleri (boş olabilir).</summary>
        public List<string> OpenedCells { get; set; } = new();
    }

    public static class LineupImpactDecisions
    {
        /// <summary>Katman ölçüldü, üretime açıldı.</summary>
        public const string Production = "Production";
        /// <summary>Katman ölçüldü, kabul kapılarını geçemedi; hesaplanıyor ama yayımlanan olasılığa uygulanmıyor.</summary>
        public const string Shadow = "Shadow";
        /// <summary>Ölçüm yapılamadı (veri yok).</summary>
        public const string NotMeasurable = "NotMeasurable";
    }

    /// <summary>
    /// KABUL KAPISI — oyuncu katmanı yalnız şu şartların HEPSİ sağlanırsa üretime çıkar:
    ///  • düzeltilmiş maç sayısı <see cref="MinAdjustedMatches"/> ve üzeri (küçük örneklemle sahte başarı yok),
    ///  • out-of-sample log loss farkı ≤ 0 (tabandan KÖTÜ değil),
    ///  • en az bir ana metrik %95 eşli aralıkla anlamlı iyileşiyor (CI üst ucu &lt; 0),
    ///  • kalibrasyon bozulmuyor (aday ECE ≤ taban ECE + <see cref="MaxCalibrationRegression"/>).
    /// </summary>
    public static class LineupImpactPolicy
    {
        public const string Version = "lineup-impact-policy-1";

        /// <summary>Kapının ölçülebilir olması için gereken en az DÜZELTİLMİŞ maç sayısı.</summary>
        public const int MinAdjustedMatches = 100;

        /// <summary>Kalibrasyonda kabul edilen en fazla gerileme.</summary>
        public const double MaxCalibrationRegression = 0.005;

        public static (string Decision, List<string> Reasons) Decide(
            LineupAblationResult candidate,
            IReadOnlyList<MarketFamilyMetrics> baseOverall)
        {
            var reasons = new List<string>();
            if (candidate.AdjustedMatches == 0)
            {
                reasons.Add("NO_ADJUSTED_MATCH");
                return (LineupImpactDecisions.NotMeasurable, reasons);
            }
            if (candidate.AdjustedMatches < MinAdjustedMatches)
                reasons.Add($"ADJUSTED_MATCHES_BELOW_{MinAdjustedMatches}:{candidate.AdjustedMatches}");

            var improved = false;
            foreach (var family in MarketFamilies.Measured)
            {
                var b = baseOverall.FirstOrDefault(x => x.Family == family);
                var c = candidate.Overall.FirstOrDefault(x => x.Family == family);
                if (b == null || c == null) continue;
                var diff = candidate.AdjustedOnlyLogLossDiff.GetValueOrDefault(family, c.LogLoss - b.LogLoss);
                var ciHigh = candidate.AdjustedOnlyCiHigh.GetValueOrDefault(family, double.MaxValue);
                if (diff > 0) reasons.Add($"WORSE_THAN_BASE:{family}:{diff:0.#####}");
                if (c.CalibrationError > b.CalibrationError + MaxCalibrationRegression)
                    reasons.Add($"CALIBRATION_REGRESSION:{family}:{b.CalibrationError:0.####}->{c.CalibrationError:0.####}");
                if (ciHigh < 0) improved = true;
            }
            if (!improved) reasons.Add("NO_SIGNIFICANT_IMPROVEMENT");

            return (reasons.Count == 0 ? LineupImpactDecisions.Production : LineupImpactDecisions.Shadow, reasons);
        }
    }

    /// <summary>
    /// KADRO KATMANI ZAMANSAL GERİYE DÖNÜK TESTİ — sızıntısız.
    ///
    /// Akış: tarihsel maçlar başlama saatine göre işlenir. Her maçta önce O MAÇTAN ÖNCEKİ bilgiyle
    /// beklenti hesaplanır (taban), sonra maçın gerçek sonucu artığa çevrilir ve ancak ondan sonra
    /// reyting modeli güncellenir. Oyuncu etki modeli her test maçı için o maçın başlama anına kadar
    /// yeniden kurulur: bir maçın kendi sonucu kendi tahminine GİREMEZ.
    ///
    /// Taban ve aday AYNI maç kümesinde karşılaştırılır; aday yalnız kadrosu doğrulanmış maçlarda
    /// tabandan ayrışır, diğerlerinde birebir aynıdır.
    /// </summary>
    public static class LineupBacktest
    {
        /// <summary>Ablasyon tanımı — kümülatif (görev şartnamesindeki 1→6 sırası).</summary>
        public sealed record Ablation(string Name, string Description, Func<PlayerImpactParameters, PlayerImpactParameters> Configure);

        public static IReadOnlyList<Ablation> Ablations { get; } = new[]
        {
            new Ablation("A2_VerifiedLineupOnly",
                "Base + doğrulanmış kadro (ham başlangıç etkisi; yedek çıkarımı, mevki kanalı ve daraltma KAPALI)",
                p => { var c = p.Clone(); c.ApplyReplacement = false; c.ApplyPositionChannel = false; c.ApplyShrinkage = false; return c; }),
            new Ablation("A3_StarterReplacementDelta",
                "Base + starter/replacement delta (yedek çıkarımı AÇIK; mevki kanalı ve daraltma kapalı)",
                p => { var c = p.Clone(); c.ApplyReplacement = true; c.ApplyPositionChannel = false; c.ApplyShrinkage = false; return c; }),
            new Ablation("A4_PositionChannel",
                "Base + pozisyon etkisi (kaleci/savunma yalnız savunma, forvet yalnız hücum kanalından)",
                p => { var c = p.Clone(); c.ApplyReplacement = true; c.ApplyPositionChannel = true; c.ApplyShrinkage = false; return c; }),
            new Ablation("A5_Shrinkage",
                "Base + belirsizlik/shrink (küçük örneklem lig/mevki ortalamasına daraltılır)",
                p => { var c = p.Clone(); c.ApplyReplacement = true; c.ApplyPositionChannel = true; c.ApplyShrinkage = true; return c; }),
            new Ablation("A6_Candidate",
                "Tüm aday model (üretim ayarları)",
                p => p.Clone()),
            new Ablation("S1_LowSampleGate",
                "DUYARLILIK: örneklem kapısı 2'ye indirildi — küçük örneklemin sahte başarı üretip üretmediğini ölçer",
                p => { var c = p.Clone(); c.MinPlayerMatches = 2; c.MinTeamMatches = 2; return c; })
        };

        private sealed record Prepared(
            EvalSample Sample,
            ScoreDistribution BaseDist,
            MatchLineupObservation? Lineup);

        public static LineupBacktestReport Run(
            IReadOnlyList<HistoricalMatch> history,
            IReadOnlyList<MatchLineupObservation> lineups,
            CompetitionCatalog catalog,
            ISet<int> lockedLeagues,
            OutcomeModelParameters modelParameters,
            PlayerImpactParameters impactParameters,
            DateTime testStartUtc,
            DateTime nowUtc,
            bool relaxProvenanceForMeasurement = false)
        {
            // Doğrulama kapısı: üretimde yapı + kaynak damgası; duyarlılık ölçümünde yalnız yapı.
            Func<MatchLineupObservation?, LineupVerificationRule.Verdict> gate = relaxProvenanceForMeasurement
                ? LineupVerificationRule.CheckStructureOnly
                : LineupVerificationRule.Check;

            var report = new LineupBacktestReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                TestStartUtc = testStartUtc,
                NowUtc = nowUtc,
                HistoryMatches = history.Count,
                LineupObservations = lineups.Count
            };

            var lineupByMatch = lineups
                .GroupBy(l => l.MatchId)
                .ToDictionary(g => g.Key, g => g.First());
            var verified = lineups.Where(l => gate(l).Accepted).ToList();
            report.ProvenanceRelaxed = relaxProvenanceForMeasurement;
            report.VerifiedObservations = verified.Count;
            report.TrainStartUtc = history.Count == 0 ? testStartUtc : history[0].KickoffUtc;

            // ── 1. TEK GEÇİŞ: taban beklentileri, artıklar ve test örnekleri ────────────────
            var model = new OutcomeRatingModel(modelParameters, catalog);
            var residuals = new List<TeamMatchResidual>(history.Count * 2);
            var prepared = new List<Prepared>();
            var notPredicted = 0;

            foreach (var m in history.OrderBy(x => x.KickoffUtc).ThenBy(x => x.MatchId))
            {
                var e = model.Expect(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);

                // Artık: yalnız KADRO GÖZLEMİ olan maçlar için üretilir (oyuncu ortalamasıyla aynı örnek uzayı).
                if (lineupByMatch.ContainsKey(m.MatchId) && e.Sufficient)
                {
                    var (h, a) = PlayerImpactModel.Residuals(m.MatchId, m.KickoffUtc, m.LeagueId, m.HomeTeamId, m.AwayTeamId,
                        m.HomeGoals, m.AwayGoals, e.LambdaHome, e.LambdaAway, impactParameters.ResidualSmoothing);
                    residuals.Add(h);
                    residuals.Add(a);
                }

                if (m.KickoffUtc >= testStartUtc && lockedLeagues.Contains(m.LeagueId))
                {
                    if (!e.Sufficient) notPredicted++;
                    else
                    {
                        var pr = OutcomePredictor.Predict(e, m.LeagueId, modelParameters);
                        var b = pr.Baseline;
                        prepared.Add(new Prepared(
                            new EvalSample(m.MatchId, m.LeagueId, m.KickoffUtc, e, m.HomeGoals, m.AwayGoals,
                                b.HomeWin, b.Draw, b.AwayWin, b.Over(2.5), b.BttsYes, b.Over(1.5), b.Over(3.5)),
                            pr.Calibrated,
                            lineupByMatch.GetValueOrDefault(m.MatchId)));
                    }
                }

                model.Update(m);
            }

            report.TestMatches = prepared.Count;
            report.TestMatchesWithLineup = prepared.Count(p => p.Lineup != null && gate(p.Lineup).Accepted);
            report.LineupCoverage = prepared.Count == 0 ? 0 : Math.Round(report.TestMatchesWithLineup / (double)prepared.Count, 4);

            // Oyuncu başına başlangıç dağılımı (kapsam dürüstlüğü).
            report.StartsPerPlayer = verified
                .SelectMany(l => l.Home.Where(x => x.Starter && x.Resolved).Concat(l.Away.Where(x => x.Starter && x.Resolved)))
                .GroupBy(x => x.PlayerKey)
                .GroupBy(g => g.Count())
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            var recentFinished = history.Count(h => h.KickoffUtc >= nowUtc.AddDays(-60));
            var baseDist = new Dictionary<int, ScoreDistribution>();
            foreach (var p in prepared) baseDist[p.Sample.MatchId] = p.BaseDist;

            var samples = prepared.Select(p => p.Sample).ToList();
            report.BaseOverall = MarketFamilyEvaluator.EvaluateAll(null, samples, s => baseDist[s.MatchId], notPredicted, recentFinished, 7717);
            report.BaseByLeague = ByLeague(samples, s => baseDist[s.MatchId], notPredicted, recentFinished);

            // ── 2. ABLASYONLAR ─────────────────────────────────────────────────────────────
            var finalImpactModel = PlayerImpactModel.BuildAsOf(verified, residuals, nowUtc, impactParameters);
            report.SufficientPlayers = finalImpactModel.SufficientPlayers;
            report.ResolvedPlayers = finalImpactModel.ResolvedPlayers;

            foreach (var ablation in Ablations)
            {
                var p = ablation.Configure(impactParameters);
                var result = new LineupAblationResult { Name = ablation.Name, Description = ablation.Description };
                var adjDist = new Dictionary<int, ScoreDistribution>();
                var deltas = new List<double>();
                var adjustedIds = new HashSet<int>();

                foreach (var item in prepared)
                {
                    var s = item.Sample;
                    var dist = item.BaseDist;
                    if (item.Lineup != null)
                    {
                        // Sızıntı güvencesi: etki modeli YALNIZ bu maçın başlama anından önceki maçları görür.
                        var impact = PlayerImpactModel.BuildAsOf(verified, residuals, s.KickoffUtc, p);
                        var match = history.First(h => h.MatchId == s.MatchId);
                        var adjustment = LineupImpactCalculator.Compute(item.Lineup, impact, match.HomeTeamId, match.AwayTeamId, gate);
                        if (adjustment.Applied)
                        {
                            var adjusted = LineupImpactCalculator.Apply(s.E, adjustment);
                            dist = OutcomePredictor.Predict(adjusted, s.LeagueId, modelParameters).Calibrated;
                            adjustedIds.Add(s.MatchId);
                            deltas.Add(Math.Max(Math.Abs(adjustment.HomeLineupDelta), Math.Abs(adjustment.AwayLineupDelta)));
                        }
                    }
                    adjDist[s.MatchId] = dist;
                }

                result.AdjustedMatches = adjustedIds.Count;
                result.MeanAbsDelta = deltas.Count == 0 ? 0 : Math.Round(deltas.Average(), 6);
                result.MaxAbsDelta = deltas.Count == 0 ? 0 : Math.Round(deltas.Max(), 6);
                result.Overall = MarketFamilyEvaluator.EvaluateAll(null, samples, s => adjDist[s.MatchId], notPredicted, recentFinished, 7717);
                result.ByLeague = ByLeague(samples, s => adjDist[s.MatchId], notPredicted, recentFinished);

                // DÜZELTİLMİŞ maçlarda aday − taban: katmanın kendi etkisi burada görülür (seyreltilmemiş).
                var adjOnly = samples.Where(s => adjustedIds.Contains(s.MatchId)).ToList();
                foreach (var family in MarketFamilies.Measured)
                {
                    var diffs = adjOnly.Select(s => Loss(family, adjDist[s.MatchId], s) - Loss(family, baseDist[s.MatchId], s)).ToArray();
                    result.AdjustedOnlyLogLossDiff[family] = diffs.Length == 0 ? 0 : Math.Round(diffs.Average(), 6);
                    var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, 4242);
                    result.AdjustedOnlyCiLow[family] = Math.Round(lo, 6);
                    result.AdjustedOnlyCiHigh[family] = Math.Round(hi, 6);
                }
                report.Ablations.Add(result);
            }

            var candidate = report.Ablations.First(a => a.Name == "A6_Candidate");
            var (decision, reasons) = LineupImpactPolicy.Decide(candidate, report.BaseOverall);
            report.Decision = decision;
            report.DecisionReasons = reasons;
            report.OpenedCells = decision == LineupImpactDecisions.Production
                ? OpenedCells(report.BaseByLeague, candidate.ByLeague)
                : new List<string>();
            return report;
        }

        /// <summary>Tabanla aday arasında UYGUNLUK DEĞİŞTİREN hücreler: kapalıyken Eligible olanlar.</summary>
        public static List<string> OpenedCells(IReadOnlyList<MarketFamilyMetrics> before, IReadOnlyList<MarketFamilyMetrics> after)
        {
            var list = new List<string>();
            foreach (var a in after.Where(x => x.Status == MarketEligibilityStatuses.Eligible))
            {
                var b = before.FirstOrDefault(x => x.LeagueId == a.LeagueId && x.Family == a.Family);
                if (b != null && b.Status != MarketEligibilityStatuses.Eligible)
                    list.Add($"{a.LeagueId}:{a.Family}");
            }
            return list;
        }

        private static List<MarketFamilyMetrics> ByLeague(
            IReadOnlyList<EvalSample> samples, Func<EvalSample, ScoreDistribution> dist, int notPredicted, int recentFinished)
        {
            var list = new List<MarketFamilyMetrics>();
            foreach (var g in samples.GroupBy(s => s.LeagueId).OrderBy(g => g.Key))
                list.AddRange(MarketFamilyEvaluator.EvaluateAll(g.Key, g.ToList(), dist, 0, recentFinished, 7717 + g.Key));
            return list;
        }

        private static double Loss(string family, ScoreDistribution d, EvalSample s) => family switch
        {
            MarketFamilies.MatchResult => GroupEvaluator.ResultLoss(d.HomeWin, d.Draw, d.AwayWin, s.HomeGoals, s.AwayGoals),
            MarketFamilies.TotalGoals15 => GroupEvaluator.BinLoss(d.Over(1.5), s.HomeGoals + s.AwayGoals > 1),
            MarketFamilies.TotalGoals25 => GroupEvaluator.BinLoss(d.Over(2.5), s.HomeGoals + s.AwayGoals > 2),
            MarketFamilies.TotalGoals35 => GroupEvaluator.BinLoss(d.Over(3.5), s.HomeGoals + s.AwayGoals > 3),
            MarketFamilies.BothTeamsToScore => GroupEvaluator.BinLoss(d.BttsYes, s.HomeGoals > 0 && s.AwayGoals > 0),
            _ => 0
        };
    }
}
