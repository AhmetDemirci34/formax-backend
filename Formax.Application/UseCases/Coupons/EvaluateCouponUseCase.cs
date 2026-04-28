using Formax.Application.Interfaces;
using Formax.Application.Services;
using Formax.Domain.Entities;
using System.Linq;

namespace Formax.Application.UseCases.Coupons
{
    public class EvaluateCouponUseCase
    {
        private readonly ICouponReadRepository _couponReadRepository;
        private readonly ICouponWriteRepository _couponWriteRepository;
        private readonly ICouponItemReadRepository _couponItemReadRepository;
        private readonly ICouponItemWriteRepository _couponItemWriteRepository;
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly AICouponCommentService _aiCouponCommentService;

        public EvaluateCouponUseCase(
            ICouponReadRepository couponReadRepository,
            ICouponWriteRepository couponWriteRepository,
            ICouponItemReadRepository couponItemReadRepository,
            ICouponItemWriteRepository couponItemWriteRepository,
            IMatchReadRepository matchReadRepository,
            AICouponCommentService aiCouponCommentService)
        {
            _couponReadRepository = couponReadRepository;
            _couponWriteRepository = couponWriteRepository;
            _couponItemReadRepository = couponItemReadRepository;
            _couponItemWriteRepository = couponItemWriteRepository;
            _matchReadRepository = matchReadRepository;
            _aiCouponCommentService = aiCouponCommentService;
        }

        public void Execute(int couponId)
        {
            var coupon = _couponReadRepository.GetById(couponId);
            if (coupon == null)
                return;

            var items = _couponItemReadRepository.GetByCouponId(coupon.Id);
            if (items == null || items.Count == 0)
                return;

            var allSuccess = true;
            var successCount = 0;

            foreach (var item in items)
            {
                var match = _matchReadRepository.GetById(item.MatchId);
                if (match == null)
                {
                    item.IsSuccess = false;
                    _couponItemWriteRepository.Update(item);
                    allSuccess = false;
                    continue;
                }

                var isSuccess = EvaluatePrediction(
                    item.PredictionTypeId,
                    match.HomeScore,
                    match.AwayScore
                );

                item.IsSuccess = isSuccess;
                _couponItemWriteRepository.Update(item);

                if (isSuccess)
                    successCount++;
                else
                    allSuccess = false;
            }

            coupon.Status = "Completed";
            coupon.Result = allSuccess ? "Win" : "Lose";

            // ✅ CONFIDENCE ARTIK DOMAIN'DEN GELMİYOR
            // ✅ SADECE USECASE İÇİ HESAPLAMA
            var confidenceRatio = items.Count > 0
                ? (double)successCount / items.Count
                : 0;

            coupon.AIComment = _aiCouponCommentService.GenerateComment(
                items,
                confidenceRatio
            );

            _couponWriteRepository.Update(coupon);
        }

        private bool EvaluatePrediction(
            int predictionTypeId,
            int homeScore,
            int awayScore)
        {
            // MVP – şimdilik tek mantık
            return homeScore > awayScore;
        }
    }
}
