using Formax.Application.Interfaces;
using Formax.Application.Live;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchEventRepository : IMatchEventRepository
    {
        private readonly FormaxDbContext _context;

        public MatchEventRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MatchEvent matchEvent)
        {
            var entity = new MatchEventEntity
            {
                MatchId = matchEvent.MatchId,
                EventType = matchEvent.EventType.ToString(),
                Minute = matchEvent.Minute,
                TeamName = matchEvent.TeamName,
                PlayerName = matchEvent.PlayerName,
                Description = matchEvent.Description,
                CreatedAt = DateTime.UtcNow
            };

            _context.MatchEvents.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<MatchEvent>> GetByMatchAsync(int matchId)
        {
            return await _context.MatchEvents
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.Minute)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new MatchEvent
                {
                    MatchId = x.MatchId,
                    EventType = Enum.Parse<MatchEventType>(x.EventType),
                    Minute = x.Minute,
                    TeamName = x.TeamName,
                    PlayerName = x.PlayerName,
                    Description = x.Description
                })
                .ToListAsync();
        }
    }
}
