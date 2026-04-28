using Formax.Application.Interfaces;
using Formax.Domain.Subscriptions;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories;


public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly FormaxDbContext _context;

    public SubscriptionRepository(FormaxDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription?> GetActiveByUserIdAsync(Guid userId)
    {
        return await _context.Subscriptions
            .FirstOrDefaultAsync(s =>
                s.UserId == userId &&
                s.IsActive(DateTime.UtcNow));
    }
}

