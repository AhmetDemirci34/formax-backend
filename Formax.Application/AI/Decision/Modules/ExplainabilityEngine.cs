using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 10 — Explainability Engine.
    ///
    /// Motorun kararını açıklanabilir kılar: neden böyle düşündüğü (reasoning), en etkili sinyaller
    /// (key drivers) ve dikkat edilecek riskler (cautions). Yalnız gerçekten üretilmiş sinyal/DNA/
    /// risk verisinden özetler; uydurma gerekçe YOK. Stateless.
    /// </summary>
    internal sealed class ExplainabilityEngine
    {
        public DecisionExplainability Explain(
            UnifiedMatchAiContext ctx,
            IReadOnlyList<TopSignal> topSignals,
            MatchDna dna,
            IReadOnlyList<AiScenario> scenarios,
            IReadOnlyList<AiRisk> risks,
            SignalField field,
            ContextIntelligence context = null,
            MatchImportance importance = null,
            SurpriseAlert surprise = null,
            IReadOnlyList<string> archetypes = null,
            ContradictionReport contradiction = null,
            DecisionConfidence confidence = null)
        {
            var reasoning = new List<string>();

            // v3 — maçın önemi + karakter arketipleri (futbol anlayışı başlığı).
            if (importance != null && importance.Score > 0)
                reasoning.Add($"Maç önemi: {importance.Score}/100 ({importance.Level}).");
            if (archetypes != null && archetypes.Count > 0)
                reasoning.Add($"Maç karakteri: {string.Join(", ", archetypes)}.");

            // v2 — maçın bağlamı (yalnız gerçek veri olan alt bloklar).
            if (context?.Explanations != null)
                reasoning.AddRange(context.Explanations);

            // v3 — upset uyarısı.
            if (surprise != null && surprise.HasAlert)
                reasoning.Add(surprise.Summary);

            // Yön özeti.
            if (Math.Abs(field.NetHomeEdge) < 0.08)
                reasoning.Add("Sinyaller güç dengesini büyük ölçüde eşit gösteriyor.");
            else
                reasoning.Add($"Ağırlıklı sinyaller {(field.NetHomeEdge > 0 ? ctx.HomeName : ctx.AwayName)} yönünde eğilimli (net edge {field.NetHomeEdge:+0.00;-0.00}).");

            // DNA öne çıkanları (en yüksek 2 boyut).
            var topDna = dna.All().OrderByDescending(d => d.Score).Take(2).ToList();
            foreach (var d in topDna)
                reasoning.Add($"{d.Name}: {d.Label} ({d.Score}/100).");

            // En güçlü senaryo.
            var best = scenarios.FirstOrDefault();
            if (best != null)
                reasoning.Add($"Öne çıkan senaryo: {best.Title} (%{best.Probability}, {best.Confidence}).");

            var keyDrivers = topSignals.Take(4).Select(t =>
                $"{t.Name} (ağırlık {t.Weight}, {(t.Direction > 0.05 ? "ev" : t.Direction < -0.05 ? "deplasman" : "yönsüz")})").ToList();

            var cautions = risks
                .OrderByDescending(r => r.Severity)
                .Take(3)
                .Select(r => $"{r.Type}: {r.Description}")
                .ToList();

            // ── v1.1 — kararı güçlendiren / zayıflatan etkenler + tek pivotal olay ──
            var favorHome = field.NetHomeEdge >= 0;

            // Güçlendiren: net yönle HİZALI (aynı işaret) en güçlü yönlü sinyaller.
            var strengthening = topSignals
                .Where(t => Math.Abs(t.Direction) > 0.05 && (t.Direction > 0) == favorHome)
                .OrderByDescending(t => t.Weight)
                .Take(3)
                .Select(t => $"{t.Name} ({(t.Direction > 0 ? "ev" : "deplasman")} yönünde, ağırlık {t.Weight}).")
                .ToList();
            if (Math.Abs(field.NetHomeEdge) >= 0.25)
                strengthening.Add($"Belirgin net edge ({field.NetHomeEdge:+0.00;-0.00}) yönü destekliyor.");

            // Zayıflatan: çelişki + yüksek risk + net yöne TERS güçlü sinyaller + düşük veri.
            var weakening = new List<string>();
            if (contradiction != null && contradiction.HasContradiction)
                weakening.Add(contradiction.Summary);
            var counterSignal = topSignals
                .Where(t => Math.Abs(t.Direction) > 0.05 && (t.Direction > 0) != favorHome)
                .OrderByDescending(t => t.Weight)
                .FirstOrDefault();
            if (counterSignal != null)
                weakening.Add($"Ters yönlü sinyal: {counterSignal.Name} ({(counterSignal.Direction > 0 ? "ev" : "deplasman")}, ağırlık {counterSignal.Weight}).");
            weakening.AddRange(risks.OrderByDescending(r => r.Severity).Where(r => r.Severity >= 60).Take(2).Select(r => $"{r.Type} riski ({r.Severity}/100)."));
            if (confidence != null && confidence.Score < 45)
                weakening.Add($"Genel güven düşük ({confidence.Score}) — veri kapsamı sınırlı.");

            // Pivotal: kararı değiştirebilecek TEK kritik olay (öncelik sırası).
            string pivotal;
            if (ctx.Availability.HasData && !ctx.Availability.LineupConfirmed)
                pivotal = "Kesin kadroların açıklanması (henüz doğrulanmadı) kararı değiştirebilir.";
            else if (contradiction != null && contradiction.HasContradiction && contradiction.Severity >= 40)
                pivotal = "Yapı–bağlam çelişkisinin çözülmesi (breaking/eksik doğrulaması) kararı çevirebilir.";
            else if ((ctx.News.HasData && ctx.News.HasBreakingNews) || (ctx.Social.HasData && ctx.Social.BreakingOfficialNews))
                pivotal = "Son saatlerdeki breaking gelişmenin netleşmesi kararı değiştirebilir.";
            else if (counterSignal != null)
                pivotal = $"{counterSignal.Name} sinyalinin güçlenmesi dengeyi çevirebilir.";
            else if (surprise != null && surprise.HasAlert)
                pivotal = "Upset senaryosunun tetiklenmesi (erken gol/kırmızı kart) kararı değiştirebilir.";
            else
                pivotal = Math.Abs(field.NetHomeEdge) < 0.12
                    ? "Denge çok ince; erken bir gol yönü belirleyebilir."
                    : "Belirgin tek çevirici olay yok; yapı görece istikrarlı.";

            return new DecisionExplainability
            {
                Reasoning = reasoning,
                KeyDrivers = keyDrivers,
                Cautions = cautions,
                StrengtheningFactors = strengthening,
                WeakeningFactors = weakening,
                PivotalFactor = pivotal
            };
        }
    }
}
