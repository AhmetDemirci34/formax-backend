namespace Formax.Application.DTOs
{
    public class CouponLiveItemDto
    {
        public int MatchId { get; set; }
        public string Status { get; set; } = null!;
        public string? MatchMinute { get; set; }
        public bool? IsSuccess { get; set; }
    }
}

