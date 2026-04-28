using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class UserWeightProfileRepository : IUserWeightProfileRepository
{
    private readonly FormaxDbContext _context;

    public UserWeightProfileRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<UserWeightProfile?> Get(int userId)
    {
        return await _context.Set<UserWeightProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task Save(UserWeightProfile profile)
    {
        var existing = await _context.Set<UserWeightProfile>()
            .FirstOrDefaultAsync(x => x.UserId == profile.UserId);

        if (existing == null)
        {
            await _context.Set<UserWeightProfile>().AddAsync(profile);
        }
        else
        {
            existing.AffinityWeight = profile.AffinityWeight;
            existing.BanditWeight = profile.BanditWeight;
            existing.ExplorationWeight = profile.ExplorationWeight;
            existing.InterestWeight = profile.InterestWeight;
            existing.UpdatedAtUtc = profile.UpdatedAtUtc;
        }

        await _context.SaveChangesAsync();
    }
}