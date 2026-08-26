using Formax.Application.Abstractions;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Formax.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;



namespace Formax.Infrastructure.Repositories;

public class MatchBanditRepository : IMatchBanditRepository
{
    private readonly IAppDbContext _context;

    public MatchBanditRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<MatchBanditStats> GetOrCreate(int matchId)
    {
        var s = await _context.MatchBanditStats
            .FirstOrDefaultAsync(x => x.MatchId == matchId);

        if (s == null)
        {
            s = new MatchBanditStats
            {
                MatchId = matchId,
                Impressions = 0,
                Likes = 0,
                UpdatedAt = DateTime.UtcNow
            };

            _context.MatchBanditStats.Add(s);
            await _context.SaveChangesAsync();
        }

        return s;
    }

    // N+1 fix: TEK sorgu + eksikler için TEK save. GetOrCreate ile birebir aynı değerler (skor değişmez).
    public async Task<Dictionary<int, MatchBanditStats>> GetOrCreateMany(IReadOnlyCollection<int> matchIds)
    {
        var ids = matchIds.Distinct().ToList();
        var map = (await _context.MatchBanditStats
                .Where(x => ids.Contains(x.MatchId))
                .ToListAsync())
            .ToDictionary(x => x.MatchId);

        var missing = ids.Where(id => !map.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var id in missing)
            {
                var s = new MatchBanditStats { MatchId = id, Impressions = 0, Likes = 0, UpdatedAt = DateTime.UtcNow };
                _context.MatchBanditStats.Add(s);
                map[id] = s;
            }
            await _context.SaveChangesAsync();
        }
        return map;
    }

    public async Task IncrementImpression(int matchId)
    {
        var s = await GetOrCreate(matchId);
        s.Impressions++;
        s.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task IncrementLike(int matchId)
    {
        var s = await GetOrCreate(matchId);
        s.Likes++;
        s.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}