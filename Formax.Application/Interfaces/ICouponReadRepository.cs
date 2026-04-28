using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ICouponReadRepository
    {
        bool Exists(int couponId);

        Coupon? GetById(int couponId);

        List<Coupon> GetByUser(int userId);

        Task<List<Coupon>> GetByUserAndResultAsync(int userId, string result);
    }
}
