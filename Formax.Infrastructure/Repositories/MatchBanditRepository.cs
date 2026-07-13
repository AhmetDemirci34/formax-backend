using Formax.Application.Abstractions;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Formax.Infrastructure.Data;



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