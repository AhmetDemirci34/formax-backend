using System;

namespace Formax.Application.AI.Limits
{
    public sealed class FatigueEvaluator
    {
        private readonly int _maxSpeaksPerHourFree = 1;
        private readonly int _maxSpeaksPerHourPremium = 3;

        public bool IsFatigueLimitReached(
            bool isPremiumUser,
            int aiUsageCountLastHour,
            DateTime? lastAiSpeakAtUtc)
        {
            // Son konuşma zamanı yoksa yorgunluk yok
            if (!lastAiSpeakAtUtc.HasValue)
                return false;

            // Son 1 saat dışında ise yorgunluk yok
            if (DateTime.UtcNow - lastAiSpeakAtUtc.Value > TimeSpan.FromHours(1))
                return false;

            var limit = isPremiumUser
                ? _maxSpeaksPerHourPremium
                : _maxSpeaksPerHourFree;

            return aiUsageCountLastHour >= limit;
        }
    }
}
