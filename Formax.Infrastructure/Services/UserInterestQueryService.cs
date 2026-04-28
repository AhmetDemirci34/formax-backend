using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Services
{
    public sealed class UserInterestQueryService : IUserInterestQueryService
    {
        private readonly FormaxDbContext _db;

        public UserInterestQueryService(FormaxDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<UserInterestDto>> GetTopAsync(
            int userId,
            string layer,
            int take)
        {
            return await _db.UserInterestScores
                .Where(x => x.UserId == userId && x.Layer == layer)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.UpdatedAtUtc)
                .Take(take)
                .Select(x => new UserInterestDto
                {
                    Key = x.Key,
                    Score = x.Score
                })
                .ToListAsync();
        }
    }
}