using Formax.Domain.States;

namespace Formax.Domain.Entities
{
    public class AIAnalysis
    {
        public int Id { get; set; }

        public int? MatchId { get; set; }
        public int? CouponId { get; set; }

        public double Probability { get; set; } // 0.40 - 0.55

        public DateTime CreatedAt { get; set; }
        public AIContextKey LastExtendedContextKey { get; set; } = AIContextKey.None;

    }
}

