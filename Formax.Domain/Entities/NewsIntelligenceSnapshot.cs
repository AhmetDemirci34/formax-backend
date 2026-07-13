using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — per-match news summary produced by
    /// deterministically matching RSS/news items to teams and their matches. One row
    /// per match (PK = MatchId).
    ///
    /// No text analysis, no AI: counts and mentions only.
    /// </summary>
    public sealed class NewsIntelligenceSnapshot
    {
        /// <summary>FK + PK → Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>Number of distinct news items linked to this match.</summary>
        public int NewsCount { get; set; }

        /// <summary>Distinct mentioned team names (JSON array).</summary>
        public string MentionedTeams { get; set; } = "[]";

        /// <summary>Distinct mentioned leagues (JSON array).</summary>
        public string MentionedLeagues { get; set; } = "[]";

        /// <summary>Timestamp of the most recent linked news item.</summary>
        public DateTime? LastNewsAtUtc { get; set; }

        /// <summary>R.10.2 — per-category counts as a JSON object, e.g.
        /// {"Injury":3,"Transfer":2}. Only non-zero categories included.</summary>
        public string CategoryBreakdown { get; set; } = "{}";

        // ── R.10.3: news impact ────────────────────────────────────────────────
        /// <summary>Normalized 0-100 news impact for the match.</summary>
        public double ImpactScore { get; set; }

        /// <summary>Banded level derived from <see cref="ImpactScore"/>.</summary>
        public NewsImpactLevel ImpactLevel { get; set; } = NewsImpactLevel.Low;

        public DateTime GeneratedAtUtc { get; set; }
    }
}
