using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class PredictionTypeReadRepository : IPredictionTypeReadRepository
    {
        private readonly FormaxDbContext _context;

        public PredictionTypeReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public bool Exists(int predictionTypeId)
        {
            return _context.PredictionTypes.Any(p => p.Id == predictionTypeId);
        }

        public PredictionType? GetById(int predictionTypeId)
        {
            return _context.PredictionTypes
                .FirstOrDefault(p => p.Id == predictionTypeId);
        }

        public List<PredictionType> GetAll()
        {
            return _context.PredictionTypes
                .OrderBy(p => p.Id)
                .ToList();
        }
    }
}
