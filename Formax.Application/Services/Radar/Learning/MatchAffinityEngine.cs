using System;
using Formax.Application.DTOs.Radar;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — default PURE engine.
    ///
    ///   Team    = max(home, away) interest + small both-teams bonus
    ///   League  = user's league interest
    ///   Signal  = weight-averaged user interest over the match's signals
    ///   userMatch = wTeam·Team + wLeague·League + wSignal·Signal
    ///   Affinity  = wUser·userMatch + wImportance·ImportanceScore   → clamp 0-100
    ///
    /// Only dictionary lookups + arithmetic. No I/O, no JSON, no persistence.
    /// </summary>
    public sealed class MatchAffinityEngine : IMatchAffinityEngine
    {
        public MatchAffinityDto Compute(UserInterestProfileDto profile, MatchAffinityContext match)
        {
            var team = TeamComponent(profile, match);
            var league = LeagueComponent(profile, match);
            var signal = SignalComponent(profile, match);

            var userMatch =
                  AffinityWeights.Team * team
                + AffinityWeights.League * league
                + AffinityWeights.Signal * signal;

            var importance = Math.Clamp(match.ImportanceScore, 0, 100);

            var affinity = AffinityWeights.User * userMatch
                         + AffinityWeights.Importance * importance;

            var score = (int)Math.Round(Math.Clamp(affinity, 0, 100));

            return new MatchAffinityDto
            {
                UserId = profile.UserId,
                MatchId = match.MatchId,
                AffinityScore = score,
                AffinityLevel = Band(score).ToString(),
                TeamComponent = (int)Math.Round(team),
                LeagueComponent = (int)Math.Round(league),
                SignalComponent = (int)Math.Round(signal),
                ImportanceComponent = (int)Math.Round(importance)
            };
        }

        private static double TeamComponent(UserInterestProfileDto p, MatchAffinityContext m)
        {
            var home = Lookup(p.Teams, m.HomeTeamName);
            var away = Lookup(p.Teams, m.AwayTeamName);
            var component = Math.Max(home, away);

            if (home > 0 && away > 0)
                component += Math.Min(AffinityWeights.BothTeamsBonusCap,
                    Math.Min(home, away) * AffinityWeights.BothTeamsBonusFactor);

            return Math.Clamp(component, 0, 100);
        }

        private static double LeagueComponent(UserInterestProfileDto p, MatchAffinityContext m)
            => Lookup(p.Leagues, m.League);

        private static double SignalComponent(UserInterestProfileDto p, MatchAffinityContext m)
        {
            if (m.Signals is null || m.Signals.Count == 0) return 0;

            double weighted = 0, totalWeight = 0;
            foreach (var s in m.Signals)
            {
                var w = s.Weight > 0 ? s.Weight : 1.0;
                weighted += Lookup(p.Signals, s.Type.ToString()) * w;
                totalWeight += w;
            }
            return totalWeight > 0 ? weighted / totalWeight : 0;
        }

        private static double Lookup(System.Collections.Generic.IReadOnlyDictionary<string, int> dict, string? key)
            => !string.IsNullOrWhiteSpace(key) && dict.TryGetValue(key, out var v) ? v : 0;

        private static MatchAffinityLevel Band(int score) => score switch
        {
            >= AffinityWeights.CriticalAt => MatchAffinityLevel.Critical,
            >= AffinityWeights.HighAt => MatchAffinityLevel.High,
            >= AffinityWeights.MediumAt => MatchAffinityLevel.Medium,
            _ => MatchAffinityLevel.Low
        };
    }
}
