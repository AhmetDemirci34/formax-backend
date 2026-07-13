using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — plain match facts the affinity engine needs. Assembled by
    /// the service (which does all I/O + JSON parse); the engine never touches EF/JSON.
    /// </summary>
    public sealed class MatchAffinityContext
    {
        public int MatchId { get; init; }

        public string HomeTeamName { get; init; } = string.Empty;
        public string AwayTeamName { get; init; } = string.Empty;
        public string League { get; init; } = string.Empty;

        /// <summary>Match signals with their weights (parsed from SignalsJson by the service).</summary>
        public IReadOnlyList<MatchAffinitySignal> Signals { get; init; } = new List<MatchAffinitySignal>();

        /// <summary>Objective match importance 0-100 (from the intelligence snapshot).</summary>
        public double ImportanceScore { get; init; }
    }

    /// <summary>One match signal (type + weight) for affinity matching.</summary>
    public sealed class MatchAffinitySignal
    {
        public MatchSignalType Type { get; init; }
        public double Weight { get; init; }
    }
}
