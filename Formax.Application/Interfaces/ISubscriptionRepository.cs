using Formax.Domain.Subscriptions;

namespace Formax.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveByUserIdAsync(Guid userId);
}
