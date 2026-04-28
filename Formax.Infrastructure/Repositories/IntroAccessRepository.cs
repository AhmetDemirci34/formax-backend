using Formax.Application.Interfaces;
using Formax.Domain.Subscriptions;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;

public class IntroAccessRepository : IIntroAccessRepository
{
    private readonly FormaxDbContext _context;

    public IntroAccessRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<IntroAccess?> GetByUserIdAsync(Guid userId)
    {
        return await _context.IntroAccesses
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }
}
