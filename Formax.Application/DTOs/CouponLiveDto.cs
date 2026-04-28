namespace Formax.Application.DTOs
{
    public class CouponLiveDto
    {
        public int CouponId { get; set; }
        public string Status { get; set; } = null!; // Active | Lost | Won
        public double Probability { get; set; }
        public List<CouponLiveItemDto> Items { get; set; } = new();
    }
}

