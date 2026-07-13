using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.2) — default visibility engine. Highest band wins:
    ///
    ///   Full   : Importance ≥ 70  OR News ≥ High  OR Synthetic ≥ High
    ///   Short  : Importance ≥ 40  OR SignalCount ≥ 3
    ///   Hidden : otherwise (Importance &lt; 40 AND SignalCount &lt; 3)
    ///
    /// Deterministic; no AI.
    /// </summary>
    public sealed class CommentaryVisibilityEngine : ICommentaryVisibilityEngine
    {
        public CommentaryVisibility Evaluate(
            double importanceScore,
            NewsImpactLevel newsImpactLevel,
            SyntheticOddsLevel syntheticSignalLevel,
            int signalCount)
        {
            if (importanceScore >= 70
                || newsImpactLevel >= NewsImpactLevel.High
                || syntheticSignalLevel >= SyntheticOddsLevel.High)
            {
                return CommentaryVisibility.Full;
            }

            if (importanceScore >= 40 || signalCount >= 3)
                return CommentaryVisibility.Short;

            return CommentaryVisibility.Hidden;
        }
    }
}
