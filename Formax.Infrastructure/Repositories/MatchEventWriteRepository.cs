using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Data.Entities;
using System;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    public sealed class MatchEventWriteRepository : IMatchEventWriteRepository
    {
        private readonly FormaxDbContext _db;

        public MatchEventWriteRepository(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(
            int matchId,
            string eventType,
            int minute,
            string teamName,
            string? playerName,
            string description)
        {
            var entity = new MatchEventEntity
            {
                MatchId = matchId,
                EventType = eventType,
                Minute = minute,
                TeamName = teamName,
                PlayerName = playerName,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _db.MatchEvents.Add(entity);
            await _db.SaveChangesAsync();
        }
    }
}