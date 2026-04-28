using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IPredictionTypeReadRepository
    {
        bool Exists(int predictionTypeId);
        PredictionType? GetById(int predictionTypeId);
        List<PredictionType> GetAll();
    }
}

