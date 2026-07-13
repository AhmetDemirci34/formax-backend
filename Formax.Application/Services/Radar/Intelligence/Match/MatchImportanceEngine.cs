using System;
using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.6) — first-class importance engine. Each of the ten
    /// factors adds points independently; the sum is clamped to 0-100 and banded into a
    /// <see cref="MatchImportanceLevel"/>. Deterministic; no AI, no LLM.
    /// </summary>
    public sealed class MatchImportanceEngine : IMatchImportanceEngine
    {
        // Factor points.
        private const double DerbyPoints = 40;
        private const double RivalryPoints = 25;
        private const double FinalPoints = 50;
        private const double PlayoffPoints = 35;
        private const double RelegationPoints = 30;
        private const double TitleRacePoints = 35;
        private const double FormGapMaxPoints = 20;
        private const double H2HIntensityMaxPoints = 20;
        private const double NewsAttentionPoints = 15;
        private const double UserAttentionPoints = 10;

        // Thresholds.
        private const double FormGapTrigger = 20;
        private const int H2HMinMeetings = 3;
        private const int NewsAttentionThreshold = 3;
        private const int UserAttentionThreshold = 3;

        private static readonly HashSet<int> BigFour = new() { 1, 2, 3, 4 };
        private static readonly (int, int) DerbyPair = (1, 2);

        public MatchImportanceResult Evaluate(MatchContextData c)
        {
            var factors = new List<MatchImportanceFactor>();
            var league = c.League ?? string.Empty;

            // ── Structural factors ────────────────────────────────────────────
            if (league.Contains("Final", StringComparison.OrdinalIgnoreCase))
                factors.Add(MatchImportanceFactor.Of("Final", FinalPoints, "competition stage: final"));

            if (league.Contains("Play", StringComparison.OrdinalIgnoreCase))
                factors.Add(MatchImportanceFactor.Of("Playoff", PlayoffPoints, "competition stage: playoff"));

            if (IsPair(c, DerbyPair.Item1, DerbyPair.Item2))
                factors.Add(MatchImportanceFactor.Of("Derby", DerbyPoints, $"{c.HomeTeamName} - {c.AwayTeamName}"));
            else if (BigFour.Contains(c.HomeTeamId) && BigFour.Contains(c.AwayTeamId))
                factors.Add(MatchImportanceFactor.Of("Rivalry", RivalryPoints, "big-four clubs"));

            if (c.HomeRank is > 0 and <= 3 && c.AwayRank is > 0 and <= 3)
                factors.Add(MatchImportanceFactor.Of("TitleRace", TitleRacePoints, $"ranks {c.HomeRank} vs {c.AwayRank}"));

            if (c.HomeRank is >= 15 && c.AwayRank is >= 15)
                factors.Add(MatchImportanceFactor.Of("Relegation", RelegationPoints, $"ranks {c.HomeRank} vs {c.AwayRank}"));

            // ── Context factors ───────────────────────────────────────────────
            if (c.HomeForm.HasData && c.AwayForm.HasData)
            {
                var gap = Math.Abs(c.HomeForm.FormScore - c.AwayForm.FormScore);
                if (gap >= FormGapTrigger)
                {
                    var pts = Math.Min(FormGapMaxPoints, gap / 100.0 * FormGapMaxPoints * 2);
                    factors.Add(MatchImportanceFactor.Of("FormGap", Math.Round(pts, 1),
                        $"form gap {gap:F0}"));
                }
            }

            if (c.H2H.Meetings >= H2HMinMeetings)
            {
                // Intensity from goal-richness + balance.
                var balance = 1.0 - (Math.Abs(c.H2H.HomeWins - c.H2H.AwayWins) / (double)c.H2H.Meetings);
                var goalFactor = Math.Min(1.0, c.H2H.AvgGoals / 3.0);
                var pts = (balance * 0.5 + goalFactor * 0.5) * H2HIntensityMaxPoints;
                factors.Add(MatchImportanceFactor.Of("H2HIntensity", Math.Round(pts, 1),
                    $"{c.H2H.Meetings} meetings, avg {c.H2H.AvgGoals}"));
            }

            // ── Enrichment factors ────────────────────────────────────────────
            var news = c.Enrichment?.NewsCount ?? 0;
            if (news >= NewsAttentionThreshold)
                factors.Add(MatchImportanceFactor.Of("NewsAttention", NewsAttentionPoints, $"news {news}"));

            var internalSignals = c.Enrichment?.InternalSignalCount ?? 0;
            if (internalSignals >= UserAttentionThreshold)
                factors.Add(MatchImportanceFactor.Of("UserAttention", UserAttentionPoints, $"internal {internalSignals}"));

            // ── R.10.4: News impact factor ────────────────────────────────────
            var newsPoints = c.NewsImpactLevel switch
            {
                NewsImpactLevel.Critical => 20,
                NewsImpactLevel.High => 10,
                NewsImpactLevel.Medium => 5,
                _ => 0
            };
            if (newsPoints > 0)
                factors.Add(MatchImportanceFactor.Of("NewsImpact", newsPoints,
                    $"news impact {c.NewsImpactLevel} ({c.NewsImpactScore})"));

            // ── R.11.4: Synthetic market attention factor ─────────────────────
            var synthPoints = c.SyntheticSignalLevel switch
            {
                SyntheticOddsLevel.Critical => 20,
                SyntheticOddsLevel.High => 10,
                SyntheticOddsLevel.Medium => 5,
                _ => 0
            };
            if (synthPoints > 0)
                factors.Add(MatchImportanceFactor.Of("SyntheticMarketAttention", synthPoints,
                    $"synthetic {c.SyntheticSignalLevel} ({c.SyntheticSignalScore})"));

            // ── Aggregate ─────────────────────────────────────────────────────
            double raw = 0;
            foreach (var f in factors) raw += f.Points;

            var score = Math.Clamp(raw, 0, 100);
            var level = Band(score);

            return new MatchImportanceResult
            {
                Score = MatchImportanceScore.Of(Math.Round(score, 1), level),
                Factors = factors,
                RawTotal = Math.Round(raw, 1)
            };
        }

        private static MatchImportanceLevel Band(double score) => score switch
        {
            >= 75 => MatchImportanceLevel.Critical,
            >= 50 => MatchImportanceLevel.High,
            >= 25 => MatchImportanceLevel.Medium,
            _ => MatchImportanceLevel.Low
        };

        private static bool IsPair(MatchContextData c, int a, int b)
            => (c.HomeTeamId == a && c.AwayTeamId == b)
            || (c.HomeTeamId == b && c.AwayTeamId == a);
    }
}
