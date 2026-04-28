using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class ContextMemoryService
    {
        private readonly FormaxDbContext _db;

        public ContextMemoryService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task UpdateContext(
            int userId,
            string? league,
            string? team,
            string? contentType,
            bool clicked,
            int dwellSeconds)
        {
            var memory = await _db.UserContextMemories
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (memory == null)
            {
                memory = new UserContextMemory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

                _db.UserContextMemories.Add(memory);
            }

            memory.LastLeague = league;
            memory.LastTeam = team;
            memory.LastContentType = contentType;

            if (clicked)
                memory.RecentClicks++;

            memory.RecentDwells += dwellSeconds;

            memory.LastInteractionAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public double CalculateContextBoost(
            UserContextMemory memory,
            string? league,
            string? team)
        {
            double boost = 0;

            if (memory.LastLeague == league)
                boost += 1.2;

            if (memory.LastTeam == team)
                boost += 1.5;

            boost += memory.RecentClicks * 0.1;
            boost += memory.RecentDwells * 0.01;

            return boost;
        }
    }
}