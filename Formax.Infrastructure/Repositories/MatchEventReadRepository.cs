using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchEventReadRepository : IMatchEventReadRepository
    {
        private readonly FormaxDbContext _context;

        public MatchEventReadRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<List<LiveMatchEventDto>> GetByMatchAsync(int matchId)
        {
            return await _context.MatchEvents
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.Minute)
                .Select(x => new LiveMatchEventDto
                {
                    Minute = x.Minute,
                    EventType = x.EventType,
                    Description = x.Description,
                    TeamName = x.TeamName,
                    PlayerName = x.PlayerName
                })
                .ToListAsync();
        }
    }
}
