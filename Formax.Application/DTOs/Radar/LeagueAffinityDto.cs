using System.Collections.Generic;

namespace Formax.Application.DTOs.Radar
{
    /// <summary>
    /// Radar Learning (R.14.4) — a user's league affinity, projected from the league
    /// dimension of their interest profile (R.14.2). First-class, read-only; computed
    /// in-memory, not persisted, not fed into ranking/recommendation.
    /// </summary>
    public sealed class LeagueAffinityDto
    {
        public int UserId { get; init; }

        /// <summary>Leagues ordered by score, highest first.</summary>
        public IReadOnlyList<LeagueAffinityItemDto> Leagues { get; init; }
            = new List<LeagueAffinityItemDto>();
    }

    /// <summary>One league's affinity score (0-100).</summary>
    public sealed class LeagueAffinityItemDto
    {
        public string League { get; init; } = string.Empty;
        public int Score { get; init; }
    }
}
