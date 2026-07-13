using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Scheduling;

/// <summary>
/// Merkezi Provider Scheduler DI kayıtları.
/// Registry ve Scheduler singleton'dır (ileride BackgroundService ile paylaşılabilmesi için).
/// Zamanlamalar (ProviderSchedule) bu fazda EKLENMEZ; onaylı provider'larla birlikte
/// <see cref="ProviderScheduleRegistry.Register"/> üzerinden tanımlanacaktır.
/// </summary>
public static class ProviderSchedulerServiceCollectionExtensions
{
    public static IServiceCollection AddProviderScheduler(this IServiceCollection services)
    {
        services.TryAddSingleton<ProviderScheduleRegistry>();
        services.TryAddSingleton<IProviderScheduler, ProviderScheduler>();
        return services;
    }
}
