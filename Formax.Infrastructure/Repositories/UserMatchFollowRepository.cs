using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class UserMatchFollowRepository : IUserMatchFollowRepository
{
    private readonly FormaxDbContext _context;

    public UserMatchFollowRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(int userId, int matchId)
    {
        return await _context.UserMatchFollows
            .AnyAsync(x => x.UserId == userId && x.MatchId == matchId);
    }

    public async Task AddAsync(UserMatchFollow follow)
    {
        _context.UserMatchFollows.Add(follow);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int userId, int matchId)
    {
        var entity = await _context.UserMatchFollows
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MatchId == matchId);

        if (entity != null)
        {
            _context.UserMatchFollows.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<UserMatchFollow>> GetByUserAsync(int userId)
    {
        return await _context.UserMatchFollows
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();
    }

    public async Task<List<UserMatchFollow>> GetByMatch(int matchId)
    {
        return await _context.UserMatchFollows
            .Where(x => x.MatchId == matchId && x.IsActive)
            .ToListAsync();
    }

    /// <inheritdoc />
    public int CountByMatchId(int matchId)
        => _context.UserMatchFollows
            .Count(x => x.MatchId == matchId && x.IsActive);
}
