using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — output of the signal engine: all fired signals
    /// (weight-ordered), the auto-selected primary signal, and a summary.
    /// </summary>
    public sealed class MatchSignalResult
    {
        public IReadOnlyList<MatchSignal> Signals { get; init; } = new List<MatchSignal>();

        public MatchSignalType PrimarySignalType { get; init; } = MatchSignalType.None;

        public string Summary { get; init; } = string.Empty;

        public bool HasSignals => Signals.Count > 0;
    }
}
