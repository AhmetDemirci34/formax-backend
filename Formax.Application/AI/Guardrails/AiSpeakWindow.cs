using System;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakWindow
    {
        private static readonly TimeSpan MinInterval =
            TimeSpan.FromMinutes(5);

        public AiSpeakWindowResult Evaluate(
            DateTime? lastSpeakAt,
            DateTime nowUtc,
            out DateTime? nextAllowedAt)
        {
            if (lastSpeakAt == null)
            {
                nextAllowedAt = null;
                return AiSpeakWindowResult.Allowed;
            }

            var diff = nowUtc - lastSpeakAt.Value;

            if (diff >= MinInterval)
            {
                nextAllowedAt = null;
                return AiSpeakWindowResult.Allowed;
            }

            nextAllowedAt = lastSpeakAt.Value + MinInterval;
            return AiSpeakWindowResult.RateLimited;
        }
    }
}
