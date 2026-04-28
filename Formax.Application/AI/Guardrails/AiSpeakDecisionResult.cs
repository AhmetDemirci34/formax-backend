using System;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakDecisionResult
    {
        public bool CanSpeak { get; init; }

        // 🔒 LEGACY / UX KÖPRÜSÜ (KALACAK)
        public string? SilenceReason { get; init; }

        // 🔒 FAZ F6 — ASIL KAYNAK
        public AiSpeakReason? SilenceReasonEnum { get; init; }

        // 🔒 FAZ-12 — typed + zaman bilgisi
        public AiSpeakReason? Reason { get; set; }
        public DateTime? NextAllowedAtUtc { get; set; }

        // 🔒 KÖPRÜLEYİCİ YARDIMCI (OPSİYONEL)
        public static AiSpeakDecisionResult Silent(AiSpeakReason reason)
        {
            return new AiSpeakDecisionResult
            {
                CanSpeak = false,
                SilenceReasonEnum = reason,
                SilenceReason = reason.ToString(),
                Reason = reason
            };
        }
    }
}
