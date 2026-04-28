namespace Formax.Application.DTOs
{
    public class CouponItemDto
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public int PredictionTypeId { get; set; }

        public bool? IsSuccess { get; set; }

        // 🔒 AI TARAFINDAN SET EDİLEN GÜVEN SKORU
        public double ConfidenceScore { get; set; }
    }
}
