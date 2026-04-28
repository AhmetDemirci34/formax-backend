using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Recommendation;

public class FeedDiversityService
{
    public List<RankedMatchResult> Apply(List<RankedMatchResult> ranked)
    {
        var result = new List<RankedMatchResult>();
        var teamCounter = new Dictionary<string, int>();

        foreach (var item in ranked)
        {
            // 🔥 GERÇEK TEAM KEY (normalize)
            var teamKey = (item.TeamA ?? "").ToLowerInvariant();

            if (!teamCounter.ContainsKey(teamKey))
                teamCounter[teamKey] = 0;

            // 🔥 SOFT LIMIT
            if (teamCounter[teamKey] >= 2)
            {
                item.Score *= 0.7;
            }

            result.Add(item);
            teamCounter[teamKey]++;

            if (result.Count >= 20)
                break;
        }

        // 🔥 yeniden sırala (çok kritik)
        return result
            .OrderByDescending(x => x.Score)
            .ToList();
    }
}