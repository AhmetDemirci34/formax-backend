using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Home;

namespace Formax.Application.Services.Recommendation.Diversity
{
    public class MatchDiversityService
    {
        public List<HomeRadarMatchDto> ApplyDiversity(List<HomeRadarMatchDto> matches)
        {
            var result = new List<HomeRadarMatchDto>();

            var usedMatches = new HashSet<int>();
            var usedTeams = new HashSet<string>();
            var usedLeagues = new HashSet<string>();

            foreach (var match in matches)
            {
                if (usedMatches.Contains(match.MatchId))
                    continue;

                if (usedTeams.Contains(match.Teams.Home) || usedTeams.Contains(match.Teams.Away))
                    continue;

                if (usedLeagues.Contains(match.League))
                    continue;

                result.Add(match);

                usedMatches.Add(match.MatchId);
                usedTeams.Add(match.Teams.Home);
                usedTeams.Add(match.Teams.Away);
                usedLeagues.Add(match.League);

                if (result.Count >= 20)
                    break;
            }

            return result;
        }
    }
}