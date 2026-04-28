using Formax.Domain.Entities;

namespace Formax.Domain.Entities
{
    public class CouponItem
    {
        public int Id { get; set; }
        public int CouponId { get; set; }
        public int MatchId { get; set; }
        public int PredictionTypeId { get; set; }
        public bool? IsSuccess { get; set; }

        // NAVIGATION PROPERTY (ŞART)
        public PredictionType? PredictionType { get; set; }
  
    }
}

