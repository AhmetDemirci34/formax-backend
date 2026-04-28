using Formax.Application.DTOs.Coupons;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Coupons
{
    public class GetCouponsByResultUseCase
    {
        private readonly ICouponReadRepository _couponReadRepository;

        public GetCouponsByResultUseCase(ICouponReadRepository couponReadRepository)
        {
            _couponReadRepository = couponReadRepository;
        }

        public async Task<List<CouponListItemDto>> ExecuteAsync(int userId, string result)
        {
            var coupons = await _couponReadRepository
                .GetByUserAndResultAsync(userId, result);

            return coupons.Select(c => new CouponListItemDto
            {
                CouponId = c.Id,
                CreatedAt = c.CreatedAt,
                Status = c.Status,
                Result = c.Result ?? string.Empty
            }).ToList();
        }
    }
}
