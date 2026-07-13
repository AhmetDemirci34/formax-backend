using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.2) — plain match facts the engine needs to resolve team/league
    /// dimensions. Assembled by the service from repositories; the engine never does I/O.
    /// </summary>
    public sealed class InterestMatchContext
    {
        public int MatchId { get; init; }
        public string HomeTeamName { get; init; } = string.Empty;
        public string AwayTeamName { get; init; } = string.Empty;
        public string League { get; init; } = string.Empty;
    }

    /// <summary>
    /// Radar Learning (R.14.2) — a single match-signal fact for the signal dimension.
    /// </summary>
    public sealed class InterestSignal
    {
        public MatchSignalType Type { get; init; }
        public bool IsPrimary { get; init; }
    }
}
