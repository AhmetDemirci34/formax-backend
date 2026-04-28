using System;
using Formax.Domain.States;

namespace Formax.Domain.Entities
{
    public class StateTransitionLog
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        public int MatchId { get; set; }

        public AIUxState FromState { get; set; }
        public AIUxState ToState { get; set; }

        // Örnekler:
        // confidence_drop
        // conflicting_signals
        // memory_decay
        // sufficient_confidence
        public string Trigger { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
