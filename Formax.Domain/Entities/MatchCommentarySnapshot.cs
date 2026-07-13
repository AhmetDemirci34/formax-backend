using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Commentary (R.12.1) — a deterministic, data-summary commentary for a match.
    /// One row per match (PK = MatchId). Built from intelligence signals, never from AI:
    /// no predictions, no "wins", no "favourite" — only a summary of the signals present.
    /// </summary>
    public sealed class MatchCommentarySnapshot
    {
        /// <summary>FK + PK → Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Short headline from the top 1-2 signals.</summary>
        public string Headline { get; set; } = string.Empty;

        /// <summary>Summary from the top 3-5 signals.</summary>
        public string Summary { get; set; } = string.Empty;

        public CommentaryTone Tone { get; set; } = CommentaryTone.Neutral;

        /// <summary>R.12.2 — visibility band; Hidden suppresses headline/summary.</summary>
        public CommentaryVisibility Visibility { get; set; } = CommentaryVisibility.Hidden;

        public DateTime GeneratedAtUtc { get; set; }
    }
}
