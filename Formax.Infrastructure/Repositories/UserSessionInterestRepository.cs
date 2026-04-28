using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories;

public class UserSessionInterestRepository : IUserSessionInterestRepository
{
    private readonly FormaxDbContext _context;

    public UserSessionInterestRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserSessionInterest?> GetAsync(int userId, string? league, string? team)
    {
        return await _context.Set<UserSessionInterest>()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.League == league &&
                x.Team == team);
    }

    public async Task UpsertAsync(UserSessionInterest entity)
    {
        var existing = await GetAsync(entity.UserId, entity.League, entity.Team);

        if (existing == null)
        {
            await _context.Set<UserSessionInterest>().AddAsync(entity);
        }
        else
        {
            existing.Clicks = entity.Clicks;
            existing.Dwells = entity.Dwells;
            existing.LastInteractionAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}