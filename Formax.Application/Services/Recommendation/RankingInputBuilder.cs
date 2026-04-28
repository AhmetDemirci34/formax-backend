using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Home;

namespace Formax.Application.Services.Recommendation;

public class RankingInputBuilder
{
    public List<RankingInput> Build(List<HomeRadarMatchDto> matches)
    {
        if (matches == null || matches.Count == 0)
            return new List<RankingInput>();

        return matches.Select(m =>
        {
            // 🔥 INTEREST (BOOSTED & BALANCED)
            var interest =
                (m.TeamInterestScore * 3.0) +
                (m.LeagueInterestScore * 2.0) +
                (m.ContentInterestScore * 1.5);

            // 🔥 UNIQUENESS (tie breaker)
            var uniqueness =
                (m.MatchId % 3 == 0 ? 5 : 0) +
                (m.MatchId % 2 == 0 ? 3 : 0);

            interest += uniqueness;

            // 🔥 SCALE (ÖNEMLİ)
            interest = Math.Max(1, interest / 5.0);

            // 🔥 TREND
            var trend = Math.Max(0, m.MatchHeatScore);

            // 🔥 FRESHNESS
            var freshness = Math.Max(0, m.TimeProximityScore);

            // 🔥 SESSION (STRONGER)
            var session = (m.BehaviorMomentumScore * 2.5) + freshness;

            // 🔥 NARRATIVE
            var narrative = Math.Max(0, m.RadarScore * 1.2);

            // 🔥 COMBINED (REFERENCE)
            var combined = interest + trend + session + narrative;

            // 🔥 BANDIT SIGNALS (REALISTIC)
            var impressions = Math.Max(1, (int)(combined / 2));

            var click = (int)(interest / 2);
            var open = (int)(interest);
            var follow = interest > 20 ? 1 : 0;

            var skip = trend < 5 ? 1 : 0;

            var dwell = Math.Max(5, session);

            return new RankingInput
            {
                MatchId = m.MatchId,

                // 🔥 ENGINE SONRA DOLDURACAK
                HomeTeamName = "",
                AwayTeamName = "",

                // 🔥 CORE
                InterestScore = interest,
                TrendScore = trend,
                NarrativeBoost = narrative,
                SessionScore = session,

                // 🔥 BANDIT
                ClickCount = click,
                OpenCount = open,
                FollowCount = follow,
                Impressions = impressions,
                SkipCount = skip,
                TotalDwellSeconds = dwell
            };
        }).ToList();
    }
}