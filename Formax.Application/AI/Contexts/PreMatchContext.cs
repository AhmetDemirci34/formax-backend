using System;

namespace Formax.Application.AI.Contexts
{
    public sealed class PreMatchContext
    {
        public int MatchId { get; init; }

        public CompetitionType CompetitionType { get; init; }
        public ImportanceLevel ImportanceLevel { get; init; }

        public bool IsDerby { get; init; }
        public bool IsElimination { get; init; }
        public bool IsFinal { get; init; }
    }

    public enum CompetitionType
    {
        League,
        ChampionsLeague,
        Cup
    }

    public enum ImportanceLevel
    {
        Low,
        Medium,
        High
    }
}
