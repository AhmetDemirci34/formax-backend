namespace Formax.Application.UseCases.Coupons;

public class CouponListItemDto
{
    public int CouponId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}
