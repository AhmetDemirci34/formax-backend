using System;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakDecisionService
    {
        private readonly AiSpeakWindow _speakWindow;

        public AiSpeakDecisionService(AiSpeakWindow speakWindow)
        {
            _speakWindow = speakWindow;
        }

        public AiSpeakDecisionResult Decide(AiSpeakDecisionInput input)
        {
            // 🔒 ÖNCELİK SIRASI (KİLİTLİ)

            if (input.ContextInsufficient)
            {
                return new AiSpeakDecisionResult
                {
                    CanSpeak = false,
                    SilenceReason = "ContextInsufficient",
                    Reason = AiSpeakReason.ContextInsufficient
                };
            }

            if (!input.StateAllowsSpeaking)
            {
                return new AiSpeakDecisionResult
                {
                    CanSpeak = false,
                    SilenceReason = "StateNotAllowed",
                    Reason = AiSpeakReason.StateBlocked
                };
            }

            if (input.FatigueLimitReached || input.RepetitionBlocked)
            {
                return new AiSpeakDecisionResult
                {
                    CanSpeak = false,
                    SilenceReason = "RateLimited",
                    Reason = AiSpeakReason.RateLimited
                };
            }

            // 🔒 FAZ-12.1 — ZAMAN / FREKANS KONTROLÜ
            var windowResult = _speakWindow.Evaluate(
                input.LastSpeakAtUtc,
                DateTime.UtcNow,
                out var nextAllowedAt);

            if (windowResult == AiSpeakWindowResult.RateLimited)
            {
                return new AiSpeakDecisionResult
                {
                    CanSpeak = false,
                    SilenceReason = "RateLimited",
                    Reason = AiSpeakReason.RateLimited,
                    NextAllowedAtUtc = nextAllowedAt
                };
            }

            // ✅ KONUŞABİLİR
            return new AiSpeakDecisionResult
            {
                CanSpeak = true,
                SilenceReason = null,
                Reason = AiSpeakReason.Allowed
            };
        }
    }
}
