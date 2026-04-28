using System;

namespace Formax.Engine.Core.Trends;

public class TrendDecayEngine
{
    public double Apply(double trendScore, DateTime lastUpdatedAt)
    {
        var minutes = (DateTime.UtcNow - lastUpdatedAt).TotalMinutes;

        if (minutes < 30)
            return trendScore;

        if (minutes < 120)
            return trendScore * 0.9;

        if (minutes < 360)
            return trendScore * 0.75;

        if (minutes < 720)
            return trendScore * 0.6;

        if (minutes < 1440)
            return trendScore * 0.45;

        return trendScore * 0.3;
    }
}