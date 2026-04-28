using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services
{
    public class CouponService
    {
        private readonly ICouponWriteRepository _writeRepository;

        public CouponService(ICouponWriteRepository writeRepository)
        {
            _writeRepository = writeRepository;
        }

        public Coupon CreateCoupon(int userId)
        {
            var coupon = new Coupon
            {
                UserId = userId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            return _writeRepository.Add(coupon);
        }
    }
}
