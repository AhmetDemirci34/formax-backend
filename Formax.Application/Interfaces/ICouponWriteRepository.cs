using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ICouponWriteRepository
    {
        Coupon Add(Coupon coupon);
        void Update(Coupon coupon);
    }
}