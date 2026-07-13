using System.Collections.Concurrent;
using Formax.Application.DTOs.Matches;

namespace Formax.Infrastructure.Cache;

public sealed class H2HCache
{
    // normalize key: smaller AF id first → 549-611 == 611-549
    private readonly ConcurrentDictionary<string, (H2HDto Data, DateTime Expiry)> _store = new();

    private static string Key(int a, int b) =>
        a < b ? $"{a}-{b}" : $"{b}-{a}";

    public bool TryGet(int homeAfId, int awayAfId, out H2HDto? data)
    {
        var key = Key(homeAfId, awayAfId);
        if (_store.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            data = entry.Data;
            return true;
        }
        _store.TryRemove(key, out _);
        data = null;
        return false;
    }

    public void Set(int homeAfId, int awayAfId, H2HDto data, int ttlSeconds = 86400)
    {
        var key = Key(homeAfId, awayAfId);
        _store[key] = (data, DateTime.UtcNow.AddSeconds(ttlSeconds));
    }
}
