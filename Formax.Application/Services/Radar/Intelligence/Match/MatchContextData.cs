using System;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — the enriched match context. No longer an empty
    /// shell: carries teams, league, ranks, both teams' form, the H2H summary, and an
    /// importance assessment. Consumed by later Match Intelligence sprints.
    /// </summary>
    public sealed class MatchContextData
    {
        public int MatchId { get; init; }

        public int HomeTeamId { get; init; }
        public int AwayTeamId { get; init; }
        public string HomeTeamName { get; init; } = string.Empty;
        public string AwayTeamName { get; init; } = string.Empty;

        public string League { get; init; } = string.Empty;
        public DateTime MatchDate { get; init; }

        public int? HomeRank { get; init; }
        public int? AwayRank { get; init; }

        public TeamFormContext HomeForm { get; init; } = new();
        public TeamFormContext AwayForm { get; init; } = new();

        public H2HContext H2H { get; init; } = new();

        public MatchImportanceContext Importance { get; init; } = new();

        /// <summary>R.9.4 — staging-derived enrichment, attached by the enricher.
        /// Null until enrichment runs; <see cref="ContextEnrichmentResult.HasEnrichment"/>
        /// is false when staging has no matching data.</summary>
        public ContextEnrichmentResult? Enrichment { get; set; }

        // ── R.10.4: News Intelligence fed into the context ─────────────────────
        /// <summary>News impact 0-100 for this match (0 when no news snapshot).</summary>
        public double NewsImpactScore { get; set; }

        public Formax.Domain.Enums.NewsImpactLevel NewsImpactLevel { get; set; }
            = Formax.Domain.Enums.NewsImpactLevel.Low;

        public int NewsCount { get; set; }

        // ── R.11.4: Synthetic Odds signal fed into the context ─────────────────
        public double SyntheticSignalScore { get; set; }

        public Formax.Domain.Enums.SyntheticOddsLevel SyntheticSignalLevel { get; set; }
            = Formax.Domain.Enums.SyntheticOddsLevel.Low;

        public Formax.Domain.Enums.OddsMovementDirection SyntheticDirection { get; set; }
            = Formax.Domain.Enums.OddsMovementDirection.Stable;
    }
}
