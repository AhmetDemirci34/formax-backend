using Formax.Domain.States;

namespace Formax.Domain.Entities
{
    public class LastExtendedContextKey
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public AIContextKey ContextKey { get; set; }

        public DateTime ExtendedAt { get; set; }
    }
}
