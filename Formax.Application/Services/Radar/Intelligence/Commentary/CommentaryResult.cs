using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.1) — engine output: a deterministic headline + summary and
    /// the tone. No AI, no prediction.
    /// </summary>
    public sealed class CommentaryResult
    {
        public string Headline { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public CommentaryTone Tone { get; init; } = CommentaryTone.Neutral;

        public bool HasContent => Headline.Length > 0;
    }
}
