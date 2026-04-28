using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.UseCases.Coupons
{
    public class AddCouponItemUseCase
    {
        private readonly ICouponItemWriteRepository _writeRepository;
        private readonly IPredictionTypeReadRepository _predictionTypeReadRepository;

        public AddCouponItemUseCase(
            ICouponItemWriteRepository writeRepository,
            IPredictionTypeReadRepository predictionTypeReadRepository)
        {
            _writeRepository = writeRepository;
            _predictionTypeReadRepository = predictionTypeReadRepository;
        }

        public void Execute(AddCouponItemRequest request)
        {
            var predictionType = _predictionTypeReadRepository
                .GetById(request.PredictionTypeId);

            if (predictionType == null)
                throw new InvalidOperationException("Prediction type not found.");

            var couponItem = new CouponItem
            {
                CouponId = request.CouponId,
                MatchId = request.MatchId,
                PredictionTypeId = request.PredictionTypeId
            };

            _writeRepository.Add(couponItem);
        }
    }
}
