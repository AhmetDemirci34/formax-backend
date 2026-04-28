using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Services.Recommendation
{
    public class SessionInterestService
    {
        private readonly FormaxDbContext _db;

        public SessionInterestService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task RegisterSessionEvent(
            int userId,
            string? league,
            string? team,
            bool clicked,
            int dwellSeconds)
        {
            var session = await _db.UserSessionInterests
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (session == null)
            {
                session = new UserSessionInterest
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

                _db.UserSessionInterests.Add(session);
            }

            session.League = league;
            session.Team = team;

            if (clicked)
                session.Clicks++;

            session.Dwells += dwellSeconds;

            session.LastInteractionAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public double CalculateSessionBoost(
            UserSessionInterest session,
            string? league,
            string? team)
        {
            double boost = 0;

            if (session.League == league)
                boost += 1.5;

            if (session.Team == team)
                boost += 2.0;

            boost += session.Clicks * 0.2;
            boost += session.Dwells * 0.02;

            return boost;
        }
    }
}