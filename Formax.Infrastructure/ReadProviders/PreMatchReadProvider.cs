using System.Threading.Tasks;
using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.ReadProviders
{
    public sealed class PreMatchReadProvider : IPreMatchReadProvider
    {
        private readonly FormaxDbContext _dbContext;

        public PreMatchReadProvider(FormaxDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PreMatchContext?> ReadAsync(int matchId)
        {
            var match = await _dbContext.Matches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == matchId);

            if (match == null)
                return null;

            return new PreMatchContext
            {
                MatchId = match.Id
                // Diğer alanlar bu fazda bilinçli olarak doldurulmuyor
            };
        }
    }
}
