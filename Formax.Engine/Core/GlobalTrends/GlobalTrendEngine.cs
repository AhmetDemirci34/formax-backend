using System;

namespace Formax.Engine.Core.GlobalTrends;

public class GlobalTrendEngine
{
    public double Calculate(double playRate, double delta)
    {
        // normalize
        var rateScore = playRate / 100.0;           // 0–1
        var deltaScore = Math.Min(delta / 20.0, 1); // cap

        // combine
        var trendScore = (rateScore * 0.6) + (deltaScore * 0.4);

        return trendScore; // 0–1
    }
}