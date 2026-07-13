using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class LeagueExternalMappingRepository : ILeagueExternalMappingRepository
    {
        private readonly FormaxDbContext _context;

        public LeagueExternalMappingRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<LeagueExternalMapping> GetAll()
            => _context.LeagueExternalMappings.ToList();

        public LeagueExternalMapping? GetByLeagueId(int leagueId)
            => _context.LeagueExternalMappings.FirstOrDefault(x => x.LeagueId == leagueId);
    }
}
