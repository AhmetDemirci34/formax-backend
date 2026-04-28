using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

public class UserPickRepository : IUserPickRepository
{
    private readonly FormaxDbContext _context;

    public UserPickRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserPick>> GetByMatchId(int matchId)
    {
        return _context.UserPicks
            .Where(x => x.MatchId == matchId)
            .ToList();
    }

    public async Task Update(UserPick pick)
    {
        _context.UserPicks.Update(pick);
        await _context.SaveChangesAsync();
    }

    public async Task Add(UserPick pick)
    {
        await _context.UserPicks.AddAsync(pick);
        await _context.SaveChangesAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}