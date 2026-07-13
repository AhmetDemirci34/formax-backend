using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.5) — default PURE engine. The interest profile already carries
    /// per-signal 0-100 scores (R.14.2, keyed by MatchSignalType name); this projects them
    /// into an ordered list and rolls them up into signal groups. A group's score is the
    /// MAX of its present member signals (the user's strongest interest in that group).
    /// Only dictionary reads + grouping. No I/O, no persistence.
    /// </summary>
    public sealed class RadarAffinityEngine : IRadarAffinityEngine
    {
        /// <summary>Signal name → group. Static taxonomy (deterministic).</summary>
        private static readonly IReadOnlyDictionary<string, string> SignalGroup =
            new Dictionary<string, string>
            {
                // Rivalry
                ["Derby"] = "Rivalry",
                ["Rivalry"] = "Rivalry",
                // Stakes
                ["TitleRace"] = "Stakes",
                ["RelegationBattle"] = "Stakes",
                ["Final"] = "Stakes",
                ["Playoff"] = "Stakes",
                ["HighImportance"] = "Stakes",
                ["ImportantMatch"] = "Stakes",
                // Form
                ["StrongForm"] = "Form",
                ["FormAdvantage"] = "Form",
                ["WeakForm"] = "Form",
                ["HistoricalDominance"] = "Form",
                ["BalancedRivalry"] = "Form",
                // News
                ["NewsAttention"] = "News",
                ["NewsMomentum"] = "News",
                ["NewsDrivenMatch"] = "News",
                // Market
                ["MarketAttention"] = "Market",
                ["SourceConfidence"] = "Market",
                ["UserAttention"] = "Market",
                ["FollowAttention"] = "Market",
            };

        public RadarAffinityDto Compute(UserInterestProfileDto profile)
        {
            var signals = profile?.Signals ?? new Dictionary<string, int>();

            var items = signals
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => new SignalAffinityItemDto { Signal = kv.Key, Score = kv.Value })
                .ToList();

            // Group rollup: MAX of present member scores; omit empty groups.
            var groups = new Dictionary<string, int>();
            foreach (var kv in signals)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                if (!SignalGroup.TryGetValue(kv.Key, out var group)) continue;
                groups[group] = groups.TryGetValue(group, out var cur) ? System.Math.Max(cur, kv.Value) : kv.Value;
            }

            var orderedGroups = groups
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return new RadarAffinityDto
            {
                UserId = profile?.UserId ?? 0,
                Signals = items,
                Groups = orderedGroups
            };
        }
    }
}
