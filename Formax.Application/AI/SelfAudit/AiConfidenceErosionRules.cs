using System;

namespace Formax.Application.AI.SelfAudit
{
    public static class AiConfidenceErosionRules
    {
        // Zaman, bağlam ve tekrar ile güveni aşındırır
        public static AiSelfAuditResult Evaluate(
            double initialConfidence,
            DateTime generatedAtUtc,
            DateTime nowUtc,
            int contextChanges,
            bool contradictingSignals)
        {
            var confidence = initialConfidence;

            // ⏱️ Zaman aşınması (her 24 saatte %5)
            var hours = (nowUtc - generatedAtUtc).TotalHours;
            confidence -= Math.Floor(hours / 24) * 0.05;

            // 🔁 Bağlam değişimi (her değişimde %7)
            confidence -= contextChanges * 0.07;

            // ⚠️ Çelişen sinyaller
            if (contradictingSignals)
                confidence -= 0.15;

            confidence = Math.Clamp(confidence, 0.0, 1.0);

            // 🔒 Eşikler
            if (confidence < 0.30)
            {
                return new AiSelfAuditResult
                {
                    IsValid = false,
                    ShouldRetract = true,
                    Reason = "ConfidenceBelowMinimum",
                    EffectiveConfidence = confidence
                };
            }

            if (confidence < 0.50)
            {
                return new AiSelfAuditResult
                {
                    IsValid = true,
                    ShouldRetract = false,
                    Reason = "ConfidenceDegraded",
                    EffectiveConfidence = confidence
                };
            }

            return new AiSelfAuditResult
            {
                IsValid = true,
                ShouldRetract = false,
                Reason = "ConfidenceStable",
                EffectiveConfidence = confidence
            };
        }
    }
}
