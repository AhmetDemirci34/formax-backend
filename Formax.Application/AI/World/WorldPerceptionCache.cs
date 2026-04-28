using System;

namespace Formax.Application.AI.World
{
    public class WorldPerceptionCache
    {
        private static readonly TimeSpan CacheDuration =
            TimeSpan.FromMinutes(5);

        private WorldPerceptionSummary? _cached;
        private DateTime? _lastUpdatedUtc;

        public void Set(WorldPerceptionSummary summary)
        {
            summary.LastUpdatedUtc = DateTime.UtcNow;
            _cached = summary;
            _lastUpdatedUtc = summary.LastUpdatedUtc;
        }

        public WorldPerceptionSummary? Get()
        {
            return _cached;
        }

        public DateTime? LastUpdatedUtc => _lastUpdatedUtc;
    }
}
