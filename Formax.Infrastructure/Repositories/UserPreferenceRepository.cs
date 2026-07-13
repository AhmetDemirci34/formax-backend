using Formax.Application.Abstractions;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Repositories;

public class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly IAppDbContext _context;

    public UserPreferenceRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<UserPreferenceWeights> GetOrCreate(int userId)
    {
        var w = await _context.UserPreferenceWeights
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (w == null)
        {
            w = new UserPreferenceWeights
            {
                UserId = userId,
                LikeWeight = 0.30,
                SkipWeight = -0.30,
                TeamWeight = 0.05,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserPreferenceWeights.Add(w);
            await _context.SaveChangesAsync();
        }

        return w;
    }

    public async Task Update(UserPreferenceWeights w)
    {
        w.UpdatedAt = DateTime.UtcNow;
        _context.UserPreferenceWeights.Update(w);
        await _context.SaveChangesAsync();
    }
}
