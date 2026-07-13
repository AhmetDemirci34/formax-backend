using System;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.3) — default synthetic engine. SignalScore is the
    /// average of the three internal scores (each 0..100), clamped to 0..100, then
    /// banded and mapped to an odds direction. Fully deterministic.
    ///
    ///   Band:  0-25 Low · 26-50 Medium · 51-75 High · 76-100 Critical
    ///   Dir:   Critical/High → Falling · Medium → Stable · Low → Rising
    /// </summary>
    public sealed class SyntheticOddsEngine : ISyntheticOddsEngine
    {
        public SyntheticOddsSignal Evaluate(
            int matchId, double interestScore, double newsImpactScore, double importanceScore)
        {
            var interest = Clamp(interestScore);
            var news = Clamp(newsImpactScore);
            var importance = Clamp(importanceScore);

            var signalScore = Math.Round((interest + news + importance) / 3.0, 1);

            var level = Band(signalScore);
            var direction = Map(level);

            return new SyntheticOddsSignal
            {
                MatchId = matchId,
                InterestScore = interest,
                NewsImpactScore = news,
                ImportanceScore = importance,
                SignalScore = signalScore,
                Level = level,
                Direction = direction
            };
        }

        private static double Clamp(double v) => Math.Clamp(v, 0, 100);

        private static SyntheticOddsLevel Band(double score) => score switch
        {
            > 75 => SyntheticOddsLevel.Critical,
            > 50 => SyntheticOddsLevel.High,
            > 25 => SyntheticOddsLevel.Medium,
            _ => SyntheticOddsLevel.Low
        };

        private static OddsMovementDirection Map(SyntheticOddsLevel level) => level switch
        {
            SyntheticOddsLevel.Critical => OddsMovementDirection.Falling,
            SyntheticOddsLevel.High => OddsMovementDirection.Falling,
            SyntheticOddsLevel.Medium => OddsMovementDirection.Stable,
            _ => OddsMovementDirection.Rising
        };
    }
}
