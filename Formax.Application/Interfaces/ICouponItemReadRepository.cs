using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ICouponItemReadRepository
    {
        List<CouponItem> GetByCouponId(int couponId);
        bool ExistsByGroup(int couponId, int matchId, string groupCode);
    }
}

