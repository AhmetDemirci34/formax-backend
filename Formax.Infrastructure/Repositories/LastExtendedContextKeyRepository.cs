using System.Linq;
using Formax.Application.Interfaces.Repositories;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class LastExtendedContextKeyRepository
        : ILastExtendedContextKeyRepository
    {
        private readonly FormaxDbContext _dbContext;

        public LastExtendedContextKeyRepository(FormaxDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public LastExtendedContextKey? GetByMatchId(int matchId)
        {
            return _dbContext.LastExtendedContextKeys
                .FirstOrDefault(x => x.MatchId == matchId);
        }

        public void Upsert(LastExtendedContextKey entity)
        {
            var existing =
                _dbContext.LastExtendedContextKeys
                    .FirstOrDefault(x => x.MatchId == entity.MatchId);

            if (existing == null)
            {
                _dbContext.LastExtendedContextKeys.Add(entity);
            }
            else
            {
                existing.ContextKey = entity.ContextKey;
                existing.ExtendedAt = entity.ExtendedAt;
            }

            _dbContext.SaveChanges();
        }
    }
}
