using System;

namespace Formax.Domain.Entities
{
    public class AIDecisionTrace
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        public int MatchId { get; set; }

        public double ConfidenceScore { get; set; }

        // Silent / Basic / Extended
        public string GuardrailDecision { get; set; } = null!;

        // Basic / Reduced / Full / Extended
        public string AiBehaviorState { get; set; } = null!;

        public bool MemoryDecayApplied { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
