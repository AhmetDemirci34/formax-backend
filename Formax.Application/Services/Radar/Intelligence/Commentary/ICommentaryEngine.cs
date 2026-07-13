using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.1) — builds a deterministic commentary from a match's
    /// intelligence snapshot (signals + importance) and its news snapshot. Pure; no AI,
    /// no LLM, no prediction.
    /// </summary>
    public interface ICommentaryEngine
    {
        CommentaryResult Generate(MatchIntelligenceSnapshot intelligence, NewsIntelligenceSnapshot? news);
    }
}
