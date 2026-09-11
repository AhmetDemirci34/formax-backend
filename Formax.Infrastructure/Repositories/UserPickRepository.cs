using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    // ── SEÇİLEBİLİR OLASI SONUÇLAR (additive) ─────────────────────────────────

    public async Task<List<UserPick>> GetByUserAndMatchAsync(
        string userId, int matchId, CancellationToken ct = default)
        => await _context.UserPicks
            .Where(x => x.UserId == userId && x.MatchId == matchId)
            .ToListAsync(ct);

    public async Task<List<UserPick>> GetByUserAsync(string userId, CancellationToken ct = default)
        => await _context.UserPicks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _context.UserPicks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return;
        _context.UserPicks.Remove(row);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveRangeAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var set = ids.ToList();
        if (set.Count == 0) return;
        var rows = await _context.UserPicks.Where(x => set.Contains(x.Id)).ToListAsync(ct);
        if (rows.Count == 0) return;
        _context.UserPicks.RemoveRange(rows);
        await _context.SaveChangesAsync(ct);
    }
}