using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — the aggregated intelligence for a match before
    /// persistence: the full signal set, the dominant signal, and a summary. The final
    /// in-memory stage before the snapshot is written.
    /// </summary>
    public sealed class MatchContext
    {
        public int MatchId { get; init; }

        public IReadOnlyList<MatchSignal> Signals { get; init; } = new List<MatchSignal>();

        public MatchSignalType PrimarySignalType { get; init; } = MatchSignalType.None;

        public string Summary { get; init; } = string.Empty;

        public bool HasSignals => Signals.Count > 0;
    }
}
