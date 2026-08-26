using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 6 — Scenario Engine (Generator + Ranking).
    ///
    /// Tek sonuç üretmez. Olasılıklardan en AYIRT EDİCİ senaryoları seçer: her market ailesinden
    /// nötr baseline'dan en çok SAPAN adayı tutar (çelişen/tekrar eden marketler elenir), sinyal
    /// gücüne göre sıralar, en güçlü 3'ü senaryo yapar; kalanlar alternatif. Neden ham olasılık
    /// değil sapma: "Çifte Şans" gibi yapısal-yüksek marketler her maçta yüksektir; sapma bu maça
    /// ÖZGÜ ayrışan tarafı öne çıkarır (ScenarioRankingService ile aynı felsefe). Stateless.
    /// </summary>
    internal sealed class ScenarioEngine
    {
        public (IReadOnlyList<AiScenario> top, IReadOnlyList<AiScenario> alternatives) Generate(
            IReadOnlyList<AiProbability> probabilities, MatchDna dna, int topN = 3)
        {
            if (probabilities == null || probabilities.Count == 0)
                return (new List<AiScenario>(), new List<AiScenario>());

            // 1) Aile başına en yüksek SAPMALI (distinctiveness) tek aday.
            var perFamilyBest = probabilities
                .GroupBy(p => p.Family)
                .Select(g => g.OrderByDescending(Distinctiveness).ThenByDescending(p => p.Probability).First())
                .ToList();

            // 2) Sapmaya göre sırala (tie: olasılık).
            var ranked = perFamilyBest
                .OrderByDescending(Distinctiveness)
                .ThenByDescending(p => p.Probability)
                .ToList();

            var top = ranked.Take(topN).Select(p => ToScenario(p, dna)).ToList();
            var alternatives = ranked.Skip(topN).Take(3).Select(p => ToScenario(p, dna)).ToList();
            return (top, alternatives);
        }

        private static AiScenario ToScenario(AiProbability p, MatchDna dna)
        {
            // Risk = maçın kaos/sürpriz DNA'sı + market belirsizliği.
            var uncertainty = 100 - Math.Abs(p.Probability - 50) * 2; // 0 (kesin) .. 100 (belirsiz)
            var riskScore = (int)Math.Clamp(
                dna.ChaosRisk.Score * 0.4 + dna.SurprisePotential.Score * 0.3 + uncertainty * 0.3, 0, 100);
            var riskTxt = riskScore >= 66 ? "Yüksek belirsizlik"
                        : riskScore >= 40 ? "Orta belirsizlik"
                        : "Düşük belirsizlik";

            return new AiScenario
            {
                Title = p.Market,
                Probability = p.Probability,
                Confidence = p.Confidence,
                Reason = p.Reason,
                Risk = $"{riskTxt} (kaos {dna.ChaosRisk.Score}, sürpriz {dna.SurprisePotential.Score}).",
                Family = p.Family
            };
        }

        // Sapma = olasılık − ailenin nötr baseline'ı. Yalnız pozitif sapma anlamlı.
        private static double Distinctiveness(AiProbability p) => p.Probability - Baseline(p);

        /// <summary>
        /// Ailenin/marketin nötr baseline'ı — sinyalsiz dengede beklenen tipik değer. Bir market
        /// ancak GERÇEK maç verisi onu bu dengenin ötesine ittiğinde öne çıkar (sabit market listesi yok).
        /// </summary>
        private static double Baseline(AiProbability p)
        {
            var m = p.Market;
            if (m.StartsWith("Ev Sahibi")) return 40;
            if (m.StartsWith("Deplasman")) return 34;
            if (m.StartsWith("Beraberlik")) return 26;
            if (m.StartsWith("Çifte Şans")) return 68;
            if (m.StartsWith("2.5 Üst")) return 50;
            if (m.StartsWith("2.5 Alt")) return 50;
            if (m.StartsWith("Karşılıklı Gol Var")) return 50;
            if (m.StartsWith("Karşılıklı Gol Yok")) return 50;
            if (m.StartsWith("İlk Yarı Ev Sahibi")) return 33;
            if (m.StartsWith("İlk Yarı Beraberlik")) return 44;
            if (m.StartsWith("İlk Yarı Deplasman")) return 25;
            if (m.StartsWith("İlk Gol Ev")) return 42;
            if (m.StartsWith("İlk Gol Deplasman")) return 38;
            if (m.StartsWith("Toplam Gol")) return 40;
            if (m.StartsWith("Skor Aralığı")) return 18;
            return 50;
        }
    }
}
