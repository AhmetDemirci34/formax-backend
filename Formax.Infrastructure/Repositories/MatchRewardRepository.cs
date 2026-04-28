using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class MatchRewardRepository : IMatchRewardRepository
{
    private readonly FormaxDbContext _db;

    public MatchRewardRepository(FormaxDbContext db)
    {
        _db = db;
    }

    public async Task UpdateAsync(int matchId, int weight)
    {
        var stats = await _db.MatchRewardStats
            .FirstOrDefaultAsync(x => x.MatchId == matchId);

        if (stats == null)
        {
            stats = new MatchRewardStats
            {
                MatchId = matchId
            };

            _db.MatchRewardStats.Add(stats);
        }

        if (weight >= 6)
            stats.OpenCount += 1;
        else if (weight >= 3)
            stats.ClickCount += 1;
        else
            stats.Impressions += 1;

        stats.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // 🔥 BURAYA EKLİYORSUN

    public async Task<MatchRewardStats?> GetByMatchIdAsync(int matchId)
    {
        return await _db.MatchRewardStats
            .FirstOrDefaultAsync(x => x.MatchId == matchId);
    }

    public async Task<List<MatchRewardStats>> GetAllAsync()
    {
        return await _db.MatchRewardStats.ToListAsync();
    }
}