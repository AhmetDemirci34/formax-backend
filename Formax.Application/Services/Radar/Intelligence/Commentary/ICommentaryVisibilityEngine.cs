using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.2) — decides how visible a match's commentary should be
    /// from its signal strength. Deterministic; no AI.
    /// </summary>
    public interface ICommentaryVisibilityEngine
    {
        CommentaryVisibility Evaluate(
            double importanceScore,
            NewsImpactLevel newsImpactLevel,
            SyntheticOddsLevel syntheticSignalLevel,
            int signalCount);
    }
}
