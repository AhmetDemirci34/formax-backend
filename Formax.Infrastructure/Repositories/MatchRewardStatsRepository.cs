using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories
{
    public class MatchRewardStatsRepository : IMatchRewardStatsRepository
    {
        private readonly FormaxDbContext _context;

        public MatchRewardStatsRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<MatchRewardStats?> GetAsync(int matchId)
        {
            return await _context.MatchRewardStats
                .FirstOrDefaultAsync(x => x.MatchId == matchId);
        }

        private async Task<MatchRewardStats> GetOrCreate(int matchId)
        {
            var stat = await GetAsync(matchId);

            if (stat == null)
            {
                stat = new MatchRewardStats
                {
                    MatchId = matchId,
                    Impressions = 1, // 🔥 0 olmasın
                    ClickCount = 0,
                    OpenCount = 0,
                    FollowCount = 0,
                    SkipCount = 0,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.MatchRewardStats.Add(stat);
                await _context.SaveChangesAsync(); // 🔥 garantile
            }

            return stat;
        }

        public async Task IncrementImpressionAsync(int matchId)
        {
            var stat = await GetOrCreate(matchId);

            stat.Impressions++;
            stat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task IncrementClickAsync(int matchId)
        {
            var stat = await GetOrCreate(matchId);

            stat.ClickCount++;
            stat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task IncrementOpenAsync(int matchId)
        {
            var stat = await GetOrCreate(matchId);

            stat.OpenCount++;
            stat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task IncrementFollowAsync(int matchId)
        {
            var stat = await GetOrCreate(matchId);

            stat.FollowCount++;
            stat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task IncrementSkipAsync(int matchId)
        {
            var stat = await GetOrCreate(matchId);

            stat.SkipCount++;
            stat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<List<MatchRewardStats>> GetAllAsync()
        {
            return await _context.MatchRewardStats
                .AsNoTracking()
                .ToListAsync();
        }
    }
}