using System.Collections.Generic;

namespace Formax.Application.DTOs.Radar
{
    /// <summary>
    /// Radar Learning (R.14.5) — a user's radar (signal) affinity, projected from the
    /// signal dimension of their interest profile (R.14.2). First-class, read-only;
    /// computed in-memory, not persisted, not fed into ranking/recommendation/feed.
    /// </summary>
    public sealed class RadarAffinityDto
    {
        public int UserId { get; init; }

        /// <summary>Signals ordered by score, highest first.</summary>
        public IReadOnlyList<SignalAffinityItemDto> Signals { get; init; }
            = new List<SignalAffinityItemDto>();

        /// <summary>Group → aggregate score (only groups with at least one present signal).</summary>
        public IReadOnlyDictionary<string, int> Groups { get; init; }
            = new Dictionary<string, int>();
    }

    /// <summary>One signal's affinity score (0-100).</summary>
    public sealed class SignalAffinityItemDto
    {
        public string Signal { get; init; } = string.Empty;
        public int Score { get; init; }
    }
}
