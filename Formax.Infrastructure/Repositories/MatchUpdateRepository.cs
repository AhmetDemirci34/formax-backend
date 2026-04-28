using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class MatchUpdateRepository : IMatchUpdateRepository
    {
        private readonly FormaxDbContext _context;

        public MatchUpdateRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public void UpdateLiveStatus(int matchId, string status, string? matchMinute)
        {
            var match = _context.Matches.FirstOrDefault(m => m.Id == matchId);

            if (match == null)
                return;

            match.Status = status;
            match.MatchMinute = matchMinute;

            _context.SaveChanges();
        }

    }
}


