using System.Collections.Generic;

namespace Formax.Application.DTOs.Radar
{
    /// <summary>
    /// Radar Learning (R.14.2) — a user's interest profile assembled from LearningEvents.
    /// Each dimension maps key → 0-100 score. Computed in-memory (no persistence in this
    /// sprint); not fed to ranking/recommendation.
    /// </summary>
    public sealed class UserInterestProfileDto
    {
        public int UserId { get; init; }

        public IReadOnlyDictionary<string, int> Teams { get; init; } = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> Leagues { get; init; } = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> Signals { get; init; } = new Dictionary<string, int>();
    }
}
