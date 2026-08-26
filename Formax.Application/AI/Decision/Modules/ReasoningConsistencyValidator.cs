using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v1.1 MODÜL — Reasoning Consistency Validator (iç denetim).
    ///
    /// Motorun kendi çıktısını çapraz denetler: olasılık ↔ DNA ↔ güven ↔ çelişki BİRBİRİYLE tutarlı mı?
    /// Örn. "2.5 Üst %70 ama Gol Potansiyeli düşük / maç kapalı" gibi iç çelişkileri yakalar. Tespit
    /// edilen tutarsızlıklar iç tutarlılık skorunu düşürür ve ilgili marketi işaretler; DÜZELTME yalnız
    /// GÜVEN düşürmedir (olasılık DEĞİŞMEZ → determinizm/hash korunur). Uydurma YOK; yalnız üretilmiş
    /// çıktı üzerinde mantık denetimi. Stateless & deterministik.
    /// </summary>
    internal sealed class ReasoningConsistencyValidator
    {
        public DecisionConsistency Validate(
            IReadOnlyList<AiProbability> probabilities, MatchDna dna, PoissonGoalModel model,
            DecisionConfidence confidence, ContradictionReport contradiction)
        {
            var issues = new List<string>();
            var corrections = new List<string>();
            var flagged = new List<string>();
            double penalty = 0;

            int Prob(string prefix)
            {
                var p = probabilities?.FirstOrDefault(x => x.Market.StartsWith(prefix, StringComparison.Ordinal));
                return p?.Probability ?? -1;
            }
            void Flag(string market, string issue, double pen)
            {
                issues.Add(issue);
                if (!string.IsNullOrEmpty(market) && !flagged.Contains(market)) flagged.Add(market);
                penalty += pen;
            }

            // 1) Üst gol ↔ gol karakteri: "2.5 Üst" yüksek ama gol potansiyeli/açıklık düşük → çelişki.
            var over = Prob("2.5 Üst");
            if (over >= 58 && (dna.GoalPotential.Score < 42 || dna.Openness.Score < 40))
                Flag("2.5 Üst", $"2.5 Üst %{over} yüksek ama Gol Potansiyeli {dna.GoalPotential.Score}/Açıklık {dna.Openness.Score} düşük.", 22);

            // 2) Alt gol ↔ bol gol karakteri: "2.5 Alt" yüksek ama gol potansiyeli yüksek → çelişki.
            var under = Prob("2.5 Alt");
            if (under >= 58 && dna.GoalPotential.Score > 68)
                Flag("2.5 Alt", $"2.5 Alt %{under} yüksek ama Gol Potansiyeli {dna.GoalPotential.Score} yüksek.", 20);

            // 3) Karşılıklı gol ↔ tek taraflı gol beklentisi: "KG Var" yüksek ama bir taraf beklenen gol < 0.75.
            var btts = Prob("Karşılıklı Gol Var");
            if (btts >= 58 && (model.ExpHome < 0.75 || model.ExpAway < 0.75))
                Flag("Karşılıklı Gol Var", $"KG Var %{btts} yüksek ama beklenen gol Ev {model.ExpHome:0.0}/Dep {model.ExpAway:0.0} tek taraflı.", 18);

            // 4) Net sonuç ↔ maç dengesi: kesin galibiyet yüksek ama DNA "dengeli maç" diyor → gerilim.
            var homeWin = Prob("Ev Sahibi Kazanır");
            var awayWin = Prob("Deplasman Kazanır");
            var decisive = Math.Max(homeWin, awayWin);
            if (decisive >= 55 && dna.Balance.Score >= 66)
                Flag(homeWin >= awayWin ? "Ev Sahibi Kazanır" : "Deplasman Kazanır",
                    $"Kesin galibiyet %{decisive} ama Maç Dengesi {dna.Balance.Score} (dengeli) — yapı gerilimli.", 14);

            // 5) Güven ↔ kaos/çelişki: yüksek güven ama yüksek kaos veya ciddi çelişki → aşırı-güven.
            if (confidence.Score >= 68 &&
                (dna.ChaosRisk.Score >= 70 || (contradiction.HasContradiction && contradiction.Severity >= 45)))
                Flag("", $"Genel güven {confidence.Score} yüksek ama kaos {dna.ChaosRisk.Score}/çelişki {(contradiction.HasContradiction ? contradiction.Severity : 0)} yüksek.", 12);

            var score = (int)Math.Clamp(100 - penalty, 0, 100);
            if (flagged.Count > 0)
                corrections.Add($"{flagged.Count} tutarsız market güveni bir kademe düşürüldü: {string.Join(", ", flagged)}.");

            return new DecisionConsistency
            {
                Score = score,
                IsConsistent = issues.Count == 0,
                Issues = issues,
                Corrections = corrections,
                FlaggedMarkets = flagged
            };
        }
    }
}
