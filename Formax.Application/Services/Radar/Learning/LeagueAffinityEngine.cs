using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.4) — default PURE engine. The interest profile already carries
    /// per-league 0-100 scores (R.14.2); this projects them into an ordered, first-class
    /// league affinity list. Only dictionary read + sort. No I/O, no persistence.
    /// </summary>
    public sealed class LeagueAffinityEngine : ILeagueAffinityEngine
    {
        public LeagueAffinityDto Compute(UserInterestProfileDto profile)
        {
            var items = (profile?.Leagues ?? new Dictionary<string, int>())
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => new LeagueAffinityItemDto { League = kv.Key, Score = kv.Value })
                .ToList();

            return new LeagueAffinityDto
            {
                UserId = profile?.UserId ?? 0,
                Leagues = items
            };
        }
    }
}
