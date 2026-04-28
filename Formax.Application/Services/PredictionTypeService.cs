using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services
{
    public class PredictionTypeService
    {
        private readonly IPredictionTypeReadRepository _predictionTypeReadRepository;

        public PredictionTypeService(IPredictionTypeReadRepository predictionTypeReadRepository)
        {
            _predictionTypeReadRepository = predictionTypeReadRepository;
        }

        public List<PredictionType> GetAll()
        {
            return _predictionTypeReadRepository.GetAll();
        }
    }
}
