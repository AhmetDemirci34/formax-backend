using Formax.Application.DTOs.Coupons;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.UseCases.Coupons
{
    public class GetCouponDetailUseCase
    {
        private readonly ICouponReadRepository _couponReadRepository;
        private readonly ICouponItemReadRepository _couponItemReadRepository;
        private readonly IPredictionTypeReadRepository _predictionTypeReadRepository;

        public GetCouponDetailUseCase(
            ICouponReadRepository couponReadRepository,
            ICouponItemReadRepository couponItemReadRepository,
            IPredictionTypeReadRepository predictionTypeReadRepository)
        {
            _couponReadRepository = couponReadRepository;
            _couponItemReadRepository = couponItemReadRepository;
            _predictionTypeReadRepository = predictionTypeReadRepository;
        }

        public CouponDetailDto Execute(int couponId)
        {
            var coupon = _couponReadRepository.GetById(couponId);
            if (coupon == null)
                throw new InvalidOperationException("Coupon not found.");

            var items = _couponItemReadRepository.GetByCouponId(couponId);

            var dto = new CouponDetailDto
            {
                CouponId = coupon.Id,
                Result = coupon.Result ?? string.Empty,
                CreatedAt = coupon.CreatedAt,
                Items = items.Select(i => new CouponItemDetailDto
                {
                    MatchId = i.MatchId,
                    PredictionCode = _predictionTypeReadRepository
                        .GetById(i.PredictionTypeId)?.Name ?? "UNKNOWN",
                    IsSuccess = i.IsSuccess
                }).ToList(),

                // 🔒 AI KUpon Yorumu (ÖNCEDEN HESAPLANMIŞ)
                AiSummary = coupon.AIComment ?? string.Empty
            };

            return dto;
        }
    }
}
