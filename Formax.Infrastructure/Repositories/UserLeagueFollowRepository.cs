using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>UserTeamFollowRepository ile aynı desen (soft-active).</summary>
    public class UserLeagueFollowRepository : IUserLeagueFollowRepository
    {
        private readonly FormaxDbContext _db;

        public UserLeagueFollowRepository(FormaxDbContext db)
        {
            _db = db;
        }

        public Task<List<UserLeagueFollow>> GetActiveByUserAsync(int userId)
        {
            return _db.Set<UserLeagueFollow>()
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();
        }

        public Task<int> CountActiveByUserAsync(int userId)
        {
            return _db.Set<UserLeagueFollow>()
                .CountAsync(x => x.UserId == userId && x.IsActive);
        }
        // Ters lookup (bildirim fan-out): bu ligi aktif takip eden kullanıcı id'leri.
        public Task<List<int>> GetUserIdsByLeagueAsync(int leagueId)
        {
            return _db.Set<UserLeagueFollow>()
                .AsNoTracking()
                .Where(x => x.LeagueId == leagueId && x.IsActive)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }

        public async Task FollowAsync(int userId, int leagueId)
        {
            var existing = await _db.Set<UserLeagueFollow>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LeagueId == leagueId);

            if (existing == null)
            {
                _db.Set<UserLeagueFollow>().Add(new UserLeagueFollow
                {
                    UserId = userId,
                    LeagueId = leagueId,
                    IsActive = true
                });
            }
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
            }
            else
            {
                return; // zaten aktif
            }

            await _db.SaveChangesAsync();
        }

        public async Task UnfollowAsync(int userId, int leagueId)
        {
            var existing = await _db.Set<UserLeagueFollow>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LeagueId == leagueId && x.IsActive);

            if (existing == null)
                return;

            existing.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }
}
