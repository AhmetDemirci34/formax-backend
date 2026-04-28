using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    public class UserTeamFollowRepository : IUserTeamFollowRepository
    {
        private readonly FormaxDbContext _db;

        public UserTeamFollowRepository(FormaxDbContext db)
        {
            _db = db;
        }

        public Task<List<UserTeamFollow>> GetActiveByUserAsync(int userId)
        {
            return _db.Set<UserTeamFollow>()
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();
        }

        // Wizard Sync: mevcut aktifleri listene göre ekle/sil
        public async Task SetUserTeamsSyncAsync(int userId, List<int> teamIds)
        {
            teamIds = teamIds.Distinct().ToList();

            var current = await _db.Set<UserTeamFollow>()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();

            var currentIds = current.Select(x => x.TeamId).Distinct().ToList();

            var toAdd = teamIds.Except(currentIds).ToList();
            var toRemove = currentIds.Except(teamIds).ToList();

            if (toRemove.Count > 0)
            {
                foreach (var item in current.Where(x => toRemove.Contains(x.TeamId)))
                    item.IsActive = false;
            }

            if (toAdd.Count > 0)
            {
                foreach (var teamId in toAdd)
                {
                    _db.Set<UserTeamFollow>().Add(new UserTeamFollow
                    {
                        UserId = userId,
                        TeamId = teamId,
                        IsActive = true
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
