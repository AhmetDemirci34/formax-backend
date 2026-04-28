using System;

namespace Formax.Engine.Core.ExternalTrends;

public class ExternalTrendEngine
{
    private readonly ExternalTrendCache _cache;

    public ExternalTrendEngine(ExternalTrendCache cache)
    {
        _cache = cache;
    }

    public ExternalTrendDto Calculate(int matchId)
    {
        if (_cache.TryGet(matchId, out var cached))
            return cached;

        var r = new Random(matchId + 999);

        var data = new ExternalTrendDto
        {
            OddsMovement = Math.Round(r.NextDouble(), 3),
            MarketConfidence = Math.Round(0.4 + r.NextDouble() * 0.6, 3),
            IsHot = r.NextDouble() > 0.7
        };

        _cache.Set(matchId, data);
        return data;
    }
}