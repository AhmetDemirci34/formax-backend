using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    public sealed class SapmaSnapshotJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(120);

        private readonly ILogger<SapmaSnapshotJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public SapmaSnapshotJob(
            ILogger<SapmaSnapshotJob> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SAPMA SNAPSHOT JOB] started"); // ✅ EKLENDİ

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SAPMA SNAPSHOT JOB] hata");
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();

            var matchRepo = scope.ServiceProvider.GetRequiredService<IMatchReadRepository>();
            var sapmaMotor = scope.ServiceProvider.GetRequiredService<ISapmaMotor>();
            var snapRepo = scope.ServiceProvider.GetRequiredService<IMatchSapmaSnapshotRepository>();

            var utcNow = DateTime.UtcNow;

            var matches = await matchRepo.GetMatchListAsync();

            if (matches == null || matches.Count == 0)
            {
                _logger.LogInformation("[SAPMA SNAPSHOT JOB] no matches to process at={Time}", utcNow); // ✅ EKLENDİ
                return;
            }

            foreach (var m in matches)
            {
                var r = sapmaMotor.CalculateForListItem(m);

                var snapshot = new MatchSapmaSnapshot
                {
                    MatchId = m.MatchId,
                    Sapma = r.Sapma,
                    Bolge = r.SapmaBolgesi,
                    SessizMi = r.SessizMi,
                    ComputedAtUtc = utcNow,
                    ExpiresAtUtc = utcNow.Add(SnapshotTtl)
                };

                await snapRepo.UpsertAsync(snapshot);
            }

            await snapRepo.SaveChangesAsync();

            _logger.LogInformation(
                "[SAPMA SNAPSHOT JOB] upsert ok. count={Count} at={Time}",
                matches.Count,
                utcNow);
        }
    }
}