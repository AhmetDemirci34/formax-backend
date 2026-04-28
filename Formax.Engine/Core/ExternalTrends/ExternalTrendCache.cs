using System.Collections.Concurrent;

namespace Formax.Engine.Core.ExternalTrends;

public class ExternalTrendCache
{
    private readonly ConcurrentDictionary<int, (ExternalTrendDto data, DateTime expiry)> _cache = new();

    public bool TryGet(int matchId, out ExternalTrendDto data)
    {
        if (_cache.TryGetValue(matchId, out var entry))
        {
            if (entry.expiry > DateTime.UtcNow)
            {
                data = entry.data;
                return true;
            }

            _cache.TryRemove(matchId, out _);
        }

        data = null!;
        return false;
    }

    public void Set(int matchId, ExternalTrendDto data, int ttlSeconds = 300)
    {
        _cache[matchId] = (data, DateTime.UtcNow.AddSeconds(ttlSeconds));
    }
}