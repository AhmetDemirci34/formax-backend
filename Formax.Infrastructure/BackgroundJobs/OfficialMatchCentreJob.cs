using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.OfficialSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// RESMÎ MAÇ MERKEZİ JOB'I — kritik gelişme tespiti (ve resmî başlama saati) için tur.
    /// Tur başına kaynak başına tek maç listesi okunur; API-Football çağrılmaz.
    /// </summary>
    public sealed class OfficialMatchCentreJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialMatchCentreJob> _log;

        public OfficialMatchCentreJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfficialMatchCentreJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        /// <summary>Tur aralığı (varsayılan 10 dk). Resmî kaynak host'ları hız sınırıyla korunur.</summary>
        private TimeSpan LoopDelay => TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("OfficialSources:MatchCentre:IntervalMinutes", 10), 5, 60));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("OfficialSources:MatchCentre:Enabled", true)) return;
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[MATCH CENTRE JOB] tur başarısız"); }
                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        public async Task<MatchCentreRoundReport> RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OfficialMatchCentreService>().RunRoundAsync(DateTime.UtcNow, ct);
        }
    }
}
