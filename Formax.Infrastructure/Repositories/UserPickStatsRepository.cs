using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

public class UserPickStatsRepository : IUserPickStatsRepository
{
    private readonly FormaxDbContext _context;

    public UserPickStatsRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserPickStats?> Get(string userId, string pickLabel)
    {
        return _context.UserPickStats
            .FirstOrDefault(x => x.UserId == userId && x.PickLabel == pickLabel);
    }

    public async Task Add(UserPickStats stats)
    {
        await _context.UserPickStats.AddAsync(stats);
        await _context.SaveChangesAsync();
    }

    public async Task Update(UserPickStats stats)
    {
        _context.UserPickStats.Update(stats);
        await _context.SaveChangesAsync();
    }
}