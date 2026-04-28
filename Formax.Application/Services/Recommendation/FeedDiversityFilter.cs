using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Recommendation;

public class FeedDiversityFilter
{
    public List<RankedMatchResult> Apply(List<RankedMatchResult> rankedMatches)
    {
        var result = new List<RankedMatchResult>();

        var usedMatches = new HashSet<int>();

        foreach (var match in rankedMatches)
        {
            if (usedMatches.Contains(match.MatchId))
                continue;

            result.Add(match);
            usedMatches.Add(match.MatchId);

            if (result.Count >= 20)
                break;
        }

        return result;
    }
}
