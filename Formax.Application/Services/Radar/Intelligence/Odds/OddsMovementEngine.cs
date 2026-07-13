using System;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.1) — default engine. Compares the home odds of two
    /// readings: direction from the sign of the change, level from the absolute delta.
    /// Fully deterministic.
    ///
    ///   current &gt; previous → Rising
    ///   current &lt; previous → Falling
    ///   equal               → Stable
    ///
    ///   |delta| 0.00–0.05 → Weak · 0.06–0.15 → Medium · 0.16+ → Strong
    /// </summary>
    public sealed class OddsMovementEngine : IOddsMovementEngine
    {
        private const double MediumThreshold = 0.06;
        private const double StrongThreshold = 0.16;

        public OddsMovementSnapshot Evaluate(OddsSnapshot previous, OddsSnapshot current)
        {
            var prev = previous.HomeOdds;
            var curr = current.HomeOdds;
            var delta = Math.Round(Math.Abs(curr - prev), 4);

            var direction = curr > prev ? OddsMovementDirection.Rising
                          : curr < prev ? OddsMovementDirection.Falling
                          : OddsMovementDirection.Stable;

            var level = delta >= StrongThreshold ? OddsMovementLevel.Strong
                      : delta >= MediumThreshold ? OddsMovementLevel.Medium
                      : OddsMovementLevel.Weak;

            return new OddsMovementSnapshot
            {
                MatchId = current.MatchId,
                PreviousOdds = prev,
                CurrentOdds = curr,
                Delta = delta,
                Direction = direction,
                Level = level,
                ComputedAtUtc = DateTime.UtcNow
            };
        }
    }
}
