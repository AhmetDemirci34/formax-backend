using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class TeamProfileSignalRepository : ITeamProfileSignalRepository
    {
        private readonly FormaxDbContext _context;

        public TeamProfileSignalRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public TeamProfileSignal? GetByTeam(int teamId)
            => _context.TeamProfileSignals
                .AsNoTracking()
                .FirstOrDefault(x => x.TeamId == teamId);

        public async Task UpsertAsync(TeamProfileSignal signal, CancellationToken ct = default)
        {
            var existing = await _context.TeamProfileSignals
                .FirstOrDefaultAsync(x => x.TeamId == signal.TeamId, ct);

            if (existing == null)
            {
                _context.TeamProfileSignals.Add(signal);
                return;
            }

            existing.ExternalTeamId     = signal.ExternalTeamId;
            existing.HasCoach           = signal.HasCoach;
            existing.CoachName          = signal.CoachName;
            existing.CoachAge           = signal.CoachAge;
            existing.HasVenue           = signal.HasVenue;
            existing.VenueName          = signal.VenueName;
            existing.VenueCity          = signal.VenueCity;
            existing.VenueCapacity      = signal.VenueCapacity;
            existing.VenueSurface       = signal.VenueSurface;
            existing.HasSquad           = signal.HasSquad;
            existing.SquadSize          = signal.SquadSize;
            existing.SquadAvgAge        = signal.SquadAvgAge;
            existing.HasTransfers       = signal.HasTransfers;
            existing.RecentTransfersIn  = signal.RecentTransfersIn;
            existing.RecentTransfersOut = signal.RecentTransfersOut;
            existing.UpdatedAt          = signal.UpdatedAt;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
