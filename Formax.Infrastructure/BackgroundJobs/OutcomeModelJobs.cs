using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// OLASI SONUÇ MODEL İŞİ — günde bir zamansal geriye dönük test + kalibrasyon koşusu (son koşu 20 saatten eskiyse), ardından
    /// 30 dk'da bir snapshot turu. Kullanıcı isteği bu işi tetiklemez; dış istek yoktur (yalnız DB).
    /// </summary>
    public sealed class OutcomeModelJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OutcomeModelJob> _log;

        public OutcomeModelJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutcomeModelJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        private TimeSpan SnapshotInterval => TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("Outcomes:SnapshotIntervalMinutes", 30), 5, 240));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("Outcomes:Enabled", true)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(_config.GetValue("Outcomes:StartupDelaySeconds", 90)), stoppingToken); }
            catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnsureTrainedAsync(stoppingToken);
                    await RunSnapshotsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[OUTCOME JOB] tur başarısız"); }
                try { await Task.Delay(SnapshotInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task EnsureTrainedAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
            var last = await db.PredictionModelRuns.AsNoTracking()
                .Where(r => r.ModelVersion == Application.Services.Outcomes.OutcomeModelVersion.Current)
                .OrderByDescending(r => r.CompletedAtUtc).Select(r => (DateTime?)r.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (last != null && DateTime.UtcNow - last.Value < TimeSpan.FromHours(20)) return;
            await scope.ServiceProvider.GetRequiredService<OutcomeModelTrainingService>().RunAsync(DateTime.UtcNow, ct);
        }

        public async Task<SnapshotCycleReport> RunSnapshotsAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<MatchPredictionSnapshotService>().RunAsync(DateTime.UtcNow, ct);
        }
    }
}
