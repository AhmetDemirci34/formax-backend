using System;

namespace Formax.Domain.Entities
{
    public class AISelfInvalidationLog
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        public int MatchId { get; set; }

        public double InitialConfidence { get; set; }
        public double EffectiveConfidence { get; set; }

        // ConfidenceBelowMinimum | ConfidenceDegraded | ContextChanged | Manual
        public string Reason { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
