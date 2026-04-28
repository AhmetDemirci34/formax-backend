using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence;

public class BanditService
{
    private readonly Random _rng = new();

    // epsilon-greedy
    public bool ShouldExplore(double explorationWeight)
    {
        // explorationWeight ~ [0,1]
        var epsilon = Math.Clamp(explorationWeight, 0.05, 0.4);
        return _rng.NextDouble() < epsilon;
    }

    // explore boost (yeni/az görülen item’ları öne çek)
    public double GetExploreBoost(int impressions)
    {
        if (impressions <= 0) return 10;     // hiç gösterilmemiş → yüksek boost
        if (impressions < 3) return 6;
        if (impressions < 7) return 3;
        return 0;
    }

    // exploit boost (iyi performanslı item’ları güçlendir)
    public double GetExploitBoost(double ctr)
    {
        // ctr: 0–1
        if (ctr > 0.6) return 8;
        if (ctr > 0.4) return 5;
        if (ctr > 0.25) return 2;
        return 0;
    }
}
