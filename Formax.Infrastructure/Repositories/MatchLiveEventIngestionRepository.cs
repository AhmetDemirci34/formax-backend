using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class MatchLiveEventIngestionRepository : IMatchLiveEventIngestionRepository
    {
        private readonly FormaxDbContext _context;

        public MatchLiveEventIngestionRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<MatchLiveEvent> GetByMatchId(int matchId)
            => _context.MatchLiveEvents
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.Minute)
                .ToList();

        public Task AddNewEventsAsync(
            int matchId,
            IEnumerable<MatchLiveEvent> events,
            CancellationToken ct = default)
        {
            // Deduplication key: (Minute, EventType, Team)
            var existing = _context.MatchLiveEvents
                .Where(x => x.MatchId == matchId)
                .Select(x => new EventKey(x.Minute, x.EventType, x.Team ?? string.Empty))
                .ToHashSet();

            foreach (var e in events)
            {
                var key = new EventKey(e.Minute, e.EventType, e.Team ?? string.Empty);

                if (existing.Add(key))   // Add returns false if already present
                    _context.MatchLiveEvents.Add(e);
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        private readonly record struct EventKey(int Minute, string EventType, string Team);
    }
}
