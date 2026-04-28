using Formax.Application.Interfaces;
using Formax.Application.UseCases.Common;
using Formax.Domain.Entities;

namespace Formax.Application.UseCases.Coupons
{
    public class CreateCouponUseCase
        : IUseCase<CreateCouponRequest, CreateCouponResponse>
    {
        private readonly ICouponWriteRepository _couponWriteRepository;

        public CreateCouponUseCase(ICouponWriteRepository couponWriteRepository)
        {
            _couponWriteRepository = couponWriteRepository;
        }

        public CreateCouponResponse Execute(CreateCouponRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var coupon = new Coupon
            {
                UserId = request.UserId,
                Status = "Draft", // MVP – ileride enum yapılabilir
                CreatedAt = DateTime.UtcNow
            };

            _couponWriteRepository.Add(coupon);

            return new CreateCouponResponse
            {
                CouponId = coupon.Id,
                Status = coupon.Status
            };
        }
    }
}
