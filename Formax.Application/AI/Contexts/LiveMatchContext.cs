using Formax.Application.Live;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// Canlı maç durumunun
    /// AI tarafından okunabilir özeti.
    /// </summary>
    public sealed class LiveMatchContext
    {
        public int CurrentMinute { get; init; }

        public int HomeScore { get; init; }
        public int AwayScore { get; init; }

        public MatchPhase MatchPhase { get; init; }
        public MatchEventType? LastEventType { get; init; }
    }

    public enum MatchPhase
    {
        FirstHalf,
        SecondHalf,
        ExtraTime
    }
}
