using Formax.Application.Services.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Formax.Infrastructure.BackgroundJobs;

public class TrainerBackgroundJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public TrainerBackgroundJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var optimizer = scope.ServiceProvider.GetService<AIWeightOptimizer>();

                if (optimizer != null)
                {
                    var result = await optimizer.Optimize();

                    Console.WriteLine("FORMAX AI Trainer Run");
                    Console.WriteLine($"ExplorationRate: {result.ExplorationRate}");
                    Console.WriteLine($"RewardWeight: {result.RewardWeight}");
                    Console.WriteLine($"RankingBoost: {result.RankingBoost}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Trainer error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

}
