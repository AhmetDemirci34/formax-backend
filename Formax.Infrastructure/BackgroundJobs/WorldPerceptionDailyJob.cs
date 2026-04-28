using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Formax.Application.AI.World;

namespace Formax.Infrastructure.BackgroundJobs
{
    public class WorldPerceptionDailyJob : BackgroundService
    {
        private readonly ILogger<WorldPerceptionDailyJob> _logger;
        private readonly WorldPerceptionCache _cache;

        public WorldPerceptionDailyJob(
            ILogger<WorldPerceptionDailyJob> logger,
            WorldPerceptionCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // 🔒 04:00 UTC batch
                if (now.Hour == 4)
                {
                    _logger.LogInformation(
                        "[WORLD PERCEPTION BATCH] 04:00 job çalıştı: {Time}",
                        now);

                    // 🔹 ŞİMDİLİK STATİK ÜRETİM
                    var summary = new WorldPerceptionSummary
                    {
                        Headline = "Günlük futbol gündemi yeniden değerlendirildi",
                        Description =
                            "Gece boyunca toplanan veriler ışığında maçların " +
                            "genel bağlamı yeniden şekillendi.",
                        ConfidenceLevel = "medium",
                        Source = "batch"
                    };

                    _cache.Set(summary);

                    // Aynı saat içinde tekrar çalışmasın
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
