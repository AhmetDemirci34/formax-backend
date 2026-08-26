using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Contradiction Engine.
    ///
    /// Yapısal güç ile bağlam/sentiment ARASINDAKİ çelişkiyi tespit eder: istatistik bir tarafı favori
    /// gösterirken o tarafta olumsuz bağlam (kesin eksikler, breaking, yüksek baskı, sağlayıcı-öngörü
    /// çelişkisi) varsa → güven DÜŞER. Yalnız gerçek sinyallerden; uydurma çelişki YOK. Stateless.
    /// </summary>
    internal sealed class ContradictionEngine
    {
        public ContradictionReport Analyze(UnifiedMatchAiContext ctx, SignalField field)
        {
            // Yapısal favori yönü (yeterince belirginse).
            var edge = field.NetHomeEdge;
            if (Math.Abs(edge) < 0.20)
                return new ContradictionReport { HasContradiction = false, Summary = "Belirgin yapısal favori yok; çelişki değerlendirilmedi." };

            var favorHome = edge > 0;
            var details = new List<string>();
            double severity = 0;

            // Favori tarafta kesin eksik (rakipten fazla) → yapıyla çelişir.
            if (ctx.Availability.HasData)
            {
                var favAbs = favorHome ? ctx.Availability.HomeKeyAbsences : ctx.Availability.AwayKeyAbsences;
                var oppAbs = favorHome ? ctx.Availability.AwayKeyAbsences : ctx.Availability.HomeKeyAbsences;
                if (favAbs > oppAbs)
                {
                    severity += Math.Min(35, (favAbs - oppAbs) * 12);
                    details.Add($"Favori tarafta {favAbs} kesin eksik (rakip {oppAbs}).");
                }
            }

            // Breaking gelişme yapısal güveni sarsar.
            if ((ctx.News.HasData && ctx.News.HasBreakingNews) || (ctx.Social.HasData && ctx.Social.BreakingOfficialNews))
            {
                severity += 20; details.Add("Son saatlerde breaking gelişme var.");
            }

            // Sağlayıcı öngörüsü yapısal favoriyle çelişiyor.
            if (ctx.Prediction.HasData)
            {
                var provEdge = (ctx.Prediction.PercentHome - ctx.Prediction.PercentAway) / 100.0;
                if (Math.Abs(provEdge) > 0.05 && Math.Sign(provEdge) != Math.Sign(edge))
                {
                    severity += 25; details.Add($"Sağlayıcı öngörüsü ters yönde ({ctx.Prediction.PercentHome}/{ctx.Prediction.PercentDraw}/{ctx.Prediction.PercentAway}).");
                }
            }

            if (severity < 15 || details.Count == 0)
                return new ContradictionReport { HasContradiction = false, Summary = "Yapı ile bağlam uyumlu; belirgin çelişki yok." };

            var sev = (int)Math.Clamp(severity, 0, 100);
            var penalty = (int)Math.Clamp(sev * 0.25, 0, 25); // güvene en fazla -25
            return new ContradictionReport
            {
                HasContradiction = true,
                Severity = sev,
                ConfidencePenalty = penalty,
                Details = details,
                Summary = $"Yapısal favori ({(favorHome ? "ev" : "deplasman")}) ile bağlam çelişiyor → güven -{penalty}."
            };
        }
    }
}
