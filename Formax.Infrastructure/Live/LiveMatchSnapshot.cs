using Formax.Application.Live;

namespace Formax.Infrastructure.Live
{
    /// <summary>
    /// Canlı maçın
    /// minimum okuma snapshot'ı.
    /// </summary>
    public sealed class LiveMatchSnapshot
    {
        public int Minute { get; init; }
        public int HomeScore { get; init; }
        public int AwayScore { get; init; }
        public MatchEventType? LastEventType { get; init; }
    }
}
