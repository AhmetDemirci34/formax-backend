using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence;

public class TrendDecayService
{
    public double ApplyDecay(
        double trendScore,
        DateTime? lastUpdatedAt
    )
    {
        if (lastUpdatedAt == null)
            return trendScore;

        var hours = (DateTime.UtcNow - lastUpdatedAt.Value).TotalHours;

        double decayFactor = hours switch
        {
            < 1 => 1.0,
            < 3 => 0.9,
            < 6 => 0.8,
            < 12 => 0.7,
            < 24 => 0.6,
            _ => 0.5
        };

        return trendScore * decayFactor;
    }
}
