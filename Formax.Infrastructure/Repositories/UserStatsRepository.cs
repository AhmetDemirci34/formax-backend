using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class UserStatsRepository : IUserStatsRepository
{
    private readonly FormaxDbContext _context;

    public UserStatsRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserStats?> Get(string userId)
    {
        return _context.UserStats.FirstOrDefault(x => x.UserId == int.Parse(userId));
    }

    public async Task Add(UserStats stats)
    {
        await _context.UserStats.AddAsync(stats);
        await _context.SaveChangesAsync();
    }

    public async Task Update(UserStats stats)
    {
        _context.UserStats.Update(stats);
        await _context.SaveChangesAsync();
    }
    public async Task<UserStats?> Get(int userId)
    {
        return await _context.UserStats
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
