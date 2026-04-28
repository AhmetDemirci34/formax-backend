using System;
using System.Collections.Concurrent;

namespace Formax.Application.AI.Limits
{
    public class AINarrativeUsageTracker
    {
        private static readonly ConcurrentDictionary<string, int> _usage
            = new();

        public bool CanConsume(Guid userId, DateTime utcNow, int dailyLimit)
        {
            var key = $"{userId}:{utcNow:yyyyMMdd}";

            _usage.TryGetValue(key, out var current);

            return current < dailyLimit;
        }

        public void Consume(Guid userId, DateTime utcNow)
        {
            var key = $"{userId}:{utcNow:yyyyMMdd}";

            _usage.AddOrUpdate(
                key,
                1,
                (_, existing) => existing + 1);
        }
    }
}
