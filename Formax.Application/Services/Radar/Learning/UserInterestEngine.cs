using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Radar;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.2) — default PURE engine.
    ///
    /// For each event it applies the deterministic weight, credits the match's two teams
    /// and league, and the match's signals (primary full, secondary half). Negative sums
    /// are floored at zero, then each dimension is normalized proportion-to-max into 0-100.
    ///
    /// No I/O of any kind — only the inputs are read; nothing is persisted.
    /// </summary>
    public sealed class UserInterestEngine : IUserInterestEngine
    {
        private const double SecondarySignalShare = 0.5;

        public UserInterestProfileDto Compute(
            int userId,
            IReadOnlyList<LearningEvent> events,
            IReadOnlyDictionary<int, InterestMatchContext> matchContext,
            IReadOnlyDictionary<int, IReadOnlyList<InterestSignal>> signalContext)
        {
            var teams = new Dictionary<string, double>();
            var leagues = new Dictionary<string, double>();
            var signals = new Dictionary<string, double>();

            if (events is not null)
            {
                foreach (var evt in events)
                {
                    var w = InterestWeights.For(evt.EventType, evt.Value);
                    if (w == 0) continue;

                    if (matchContext.TryGetValue(evt.MatchId, out var m))
                    {
                        Add(teams, m.HomeTeamName, w);
                        Add(teams, m.AwayTeamName, w);
                        Add(leagues, m.League, w);
                    }

                    if (signalContext.TryGetValue(evt.MatchId, out var sigs))
                    {
                        foreach (var s in sigs)
                        {
                            var share = s.IsPrimary ? 1.0 : SecondarySignalShare;
                            Add(signals, s.Type.ToString(), w * share);
                        }
                    }
                }
            }

            return new UserInterestProfileDto
            {
                UserId = userId,
                Teams = Normalize(teams),
                Leagues = Normalize(leagues),
                Signals = Normalize(signals)
            };
        }

        private static void Add(Dictionary<string, double> dict, string? key, double w)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            dict[key] = dict.GetValueOrDefault(key) + w;
        }

        /// <summary>Drop ≤0 sums, then proportion-to-max → 0-100 (rounded int).</summary>
        private static IReadOnlyDictionary<string, int> Normalize(Dictionary<string, double> raw)
        {
            var positive = raw.Where(kv => kv.Value > 0).ToList();
            if (positive.Count == 0) return new Dictionary<string, int>();

            var max = positive.Max(kv => kv.Value);
            return positive
                .OrderByDescending(kv => kv.Value)
                .ToDictionary(
                    kv => kv.Key,
                    kv => (int)Math.Round(kv.Value / max * 100.0));
        }
    }
}
