using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    public sealed class OutcomeChangeLine
    {
        public string Market { get; set; } = string.Empty;
        public double Old { get; set; }
        public double New { get; set; }
        public double Delta { get; set; }
        public double Allowed { get; set; }
        public bool Exceeded { get; set; }
    }

    public sealed class OutcomeChangeAudit
    {
        public string? PreviousSnapshotId { get; set; }
        public string TriggerType { get; set; } = "Periodic";
        public string? TriggerSource { get; set; }
        public DateTime? TriggeredAtUtc { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string? PreviousModelVersion { get; set; }
        public string? CalibrationRunId { get; set; }
        public string? PreviousCalibrationRunId { get; set; }
        /// <summary>Önceki snapshot'ın girdi kesiminden bu yana iki takımın oynadığı yeni bitmiş maç sayısı.</summary>
        public int NewFinishedMatches { get; set; }
        /// <summary>Aynı sürede maçın organizasyonunda (lig/turnuva) biten yeni maç sayısı (gol tabanını ve lig güçlerini günceller).</summary>
        public int NewCompetitionMatches { get; set; }
        public double CompetitionBaseLogShift { get; set; }
        public double OldEvidenceCoverage { get; set; }
        public double NewEvidenceCoverage { get; set; }
        public string? OldEligibility { get; set; }
        public string? NewEligibility { get; set; }
        /// <summary>Yeni girdiler (resmî kadro, kritik gelişme, yeni bitmiş maç, lig gücü değişimi...).</summary>
        public List<string> NewInputs { get; set; } = new();
        /// <summary>Doğrulanmış oyuncu etki verisinden gelen izin (şu an doğrulanmış oyuncu etki modeli YOK → 0).</summary>
        public double ValidatedImpactAllowance { get; set; }
        public double RatingDriftLogShift { get; set; }
        public List<OutcomeChangeLine> Lines { get; set; } = new();
        /// <summary>Published | NeedsReview.</summary>
        public string Decision { get; set; } = "Published";
        public string? DecisionReason { get; set; }
    }

    /// <summary>
    /// TAHMİN DEĞİŞİM KAPISI — yeni snapshot yüzdeleri önceki YAYIMLANMIŞ snapshot'a göre açıklanabilir sınır içinde mi?
    ///
    /// Sınır tek sabit değildir: yeni girdilerin taşıyabileceği en büyük etkiden türetilir.
    ///  • Reyting kayması: log λ üzerinde δ = öğrenme oranı × gol artığı std × √(2·yeni maç + 1) × (1 + belirsizlik) + |Δ lig gücü farkı|/2;
    ///    olasılık sınırı, beklenen golleri ±δ (aynı ve zıt yönde) kaydırıp aynı kalibre dağılımdan ölçülür.
    ///  • Oyuncu/kadro etkisi: yalnız doğrulanmış oyuncu etki verisi varsa eklenir (şu an yok → 0; kadro haberi olasılığı değiştiremez).
    ///  • Yuvarlama/kalibrasyon toleransı: 0,02.
    /// Model sürümü ya da kalibrasyon koşusu değiştiyse değişim sınırlanmaz (model backtest'ten geçmiştir) ama denetime yazılır.
    /// </summary>
    public static class OutcomeChangeGate
    {
        public const double RoundingTolerance = 0.02;

        public static OutcomeChangeAudit Evaluate(
            OutcomeSnapshotDto previous, OutcomeSnapshotDto next, OutcomeExpectation nextExpectation, int leagueId,
            OutcomeModelParameters p, int newFinishedMatches, string triggerType, string? triggerSource, DateTime triggeredAtUtc,
            IEnumerable<string> newInputs, int newCompetitionMatches = 0)
        {
            var audit = new OutcomeChangeAudit
            {
                PreviousSnapshotId = previous.SnapshotId, TriggerType = triggerType, TriggerSource = triggerSource, TriggeredAtUtc = triggeredAtUtc,
                ModelVersion = next.ModelVersion, PreviousModelVersion = previous.ModelVersion,
                CalibrationRunId = next.CalibrationRunId, PreviousCalibrationRunId = previous.CalibrationRunId,
                NewFinishedMatches = newFinishedMatches, NewCompetitionMatches = newCompetitionMatches, OldEvidenceCoverage = previous.EvidenceCoverage, NewEvidenceCoverage = next.EvidenceCoverage,
                OldEligibility = previous.PredictionEligibility, NewEligibility = next.PredictionEligibility,
                NewInputs = newInputs.ToList()
            };

            var oldP = Probabilities(previous);
            var newP = Probabilities(next);
            if (oldP.Count == 0 || newP.Count == 0)
            {
                audit.DecisionReason = "NO_COMPARABLE_PROBABILITIES";
                return audit;
            }

            var modelChanged = previous.ModelVersion != next.ModelVersion || previous.CalibrationRunId != next.CalibrationRunId;
            var strengthShift = previous.Strength != null && next.Strength != null && next.Strength.CrossLeague
                ? Math.Abs((next.Strength.HomeLeagueStrength - next.Strength.AwayLeagueStrength) - (previous.Strength.HomeLeagueStrength - previous.Strength.AwayLeagueStrength)) / 2
                : 0;
            // Organizasyon gol tabanı: iki snapshot'ta ölçülen gerçek kayma; eski snapshot tabanı taşımıyorsa organizasyondaki yeni maç
            // sayısından üst sınır (her maç tabanı en fazla α × artık std kadar oynatır). Ölçüm 17.09.2026: 9 yeni UEFA sonucu
            // Juventus–NEC gol marketlerini ~%5 kaydırdı, bu terim yokken yanlışlıkla NeedsReview oluyordu.
            double baseShift;
            if (previous.Strength is { LeagueHome: > 0, LeagueAway: > 0 } ps && next.Strength is { LeagueHome: > 0, LeagueAway: > 0 } ns)
                baseShift = Math.Max(Math.Abs(Math.Log(ns.LeagueHome / ps.LeagueHome)), Math.Abs(Math.Log(ns.LeagueAway / ps.LeagueAway)));
            else
                baseShift = Math.Min(0.5, Math.Max(0, newCompetitionMatches) * Math.Max(p.LeagueAlpha, 0.02) * p.GoalResidualStd);
            audit.CompetitionBaseLogShift = Math.Round(baseShift, 4);
            var delta = p.LearningRate * p.GoalResidualStd * Math.Sqrt(2.0 * Math.Max(0, newFinishedMatches) + 1) * (1 + (1 - nextExpectation.Coverage)) + strengthShift + baseShift;
            audit.RatingDriftLogShift = Math.Round(delta, 4);
            var allowed = AllowedShift(nextExpectation, leagueId, p, delta);

            foreach (var (market, nv) in newP)
            {
                if (!oldP.TryGetValue(market, out var ov)) continue;
                var line = new OutcomeChangeLine
                {
                    Market = market, Old = Math.Round(ov, 4), New = Math.Round(nv, 4), Delta = Math.Round(nv - ov, 4),
                    Allowed = Math.Round(allowed.GetValueOrDefault(market) + RoundingTolerance + audit.ValidatedImpactAllowance, 4)
                };
                line.Exceeded = Math.Abs(nv - ov) > line.Allowed + 1e-9;
                audit.Lines.Add(line);
            }

            if (modelChanged)
            {
                audit.DecisionReason = "MODEL_OR_CALIBRATION_UPDATE";
                return audit;
            }
            if (audit.Lines.Any(l => l.Exceeded))
            {
                audit.Decision = "NeedsReview";
                audit.DecisionReason = "UNEXPLAINED_PROBABILITY_CHANGE";
            }
            return audit;
        }

        /// <summary>Kullanıcıya gösterilen üç ailenin kalibre olasılıkları (aile öğelerinden).</summary>
        public static Dictionary<string, double> Probabilities(OutcomeSnapshotDto s)
        {
            var map = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var f in s.Families)
                foreach (var i in f.Items)
                    if (f.Family is OutcomeFamilies.Result or OutcomeFamilies.Goals or OutcomeFamilies.Btts)
                        map[i.Market] = i.CalibratedProbability;
            return map;
        }

        private static Dictionary<string, double> AllowedShift(OutcomeExpectation e, int leagueId, OutcomeModelParameters p, double delta)
        {
            var baseD = OutcomePredictor.Predict(e, leagueId, p).Calibrated;
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (sh, sa) in new[] { (1, -1), (-1, 1), (1, 1), (-1, -1) })
            {
                var moved = e with { LambdaHome = e.LambdaHome * Math.Exp(sh * delta), LambdaAway = e.LambdaAway * Math.Exp(sa * delta) };
                var d = OutcomePredictor.Predict(moved, leagueId, p).Calibrated;
                void Put(string m, double a, double b) => result[m] = Math.Max(result.GetValueOrDefault(m), Math.Abs(a - b));
                Put("Ev Sahibi Kazanır", d.HomeWin, baseD.HomeWin);
                Put("Beraberlik", d.Draw, baseD.Draw);
                Put("Deplasman Kazanır", d.AwayWin, baseD.AwayWin);
                foreach (var line in new[] { 1.5, 2.5, 3.5 })
                {
                    var l = line.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                    Put($"{l} Üst", d.Over(line), baseD.Over(line));
                    Put($"{l} Alt", d.Under(line), baseD.Under(line));
                }
                Put("Karşılıklı Gol Var", d.BttsYes, baseD.BttsYes);
                Put("Karşılıklı Gol Yok", d.BttsNo, baseD.BttsNo);
            }
            return result;
        }
    }
}
