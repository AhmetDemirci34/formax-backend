using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.MatchAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// AI MAÇ ANALİZİ JOB'I — yaklaşan maçların analizini ÖNCEDEN üretir (kullanıcı sayfası LLM
    /// çağırmaz, yalnız bu job'ın yazdığı kaydı okur).
    ///
    /// Tur: 20 dk. Aday: kapsam içi, başlamamış, 72 saat içindeki maçlar. Kanıt özeti değişmediyse
    /// (sonuç/kadro/puan durumu aynı) maç yeniden üretilmez. LLM çevirisi tur başına tavanlıdır.
    /// </summary>
    public sealed class MatchAnalysisJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(20);
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchAnalysisJob> _log;

        public MatchAnalysisJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<MatchAnalysisJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("MatchAnalysis:Enabled", true)) return;
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(null, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[ANALYSIS JOB] tur başarısız"); }
                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        /// <summary>Bir tur. <paramref name="maxMatches"/> verilirse admin tetiğidir.</summary>
        public async Task<System.Collections.Generic.List<AnalysisGenerationResult>> RunCycleAsync(
            int? maxMatches, CancellationToken ct, bool force = false, int horizonHours = 72)
        {
            using var _ = LlmCallMeter.Begin("background:" + nameof(MatchAnalysisJob));
            var results = new System.Collections.Generic.List<AnalysisGenerationResult>();

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var now = DateTime.UtcNow;
            var ids = (await db.Matches.AsNoTracking()
                    .Where(m => m.MatchDate > now && m.MatchDate <= now.AddHours(horizonHours) && m.Status == MatchStatuses.NotStarted)
                    .OrderBy(m => m.MatchDate)
                    .Select(m => new { m.Id, m.LeagueId })
                    .ToListAsync(ct))
                .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                .Select(m => m.Id)
                .Take(maxMatches ?? _config.GetValue("MatchAnalysis:MaxMatchesPerCycle", 60))
                .ToList();

            var llmBudget = _config.GetValue("MatchAnalysis:MaxLlmCallsPerCycle", 20);
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                // Her maç kendi kapsamında: bir maçın hatası turu düşürmez, EF izleyicisi şişmez.
                using var matchScope = _scopes.CreateScope();
                var generator = matchScope.ServiceProvider.GetRequiredService<MatchAnalysisGenerator>();
                try
                {
                    var r = await generator.GenerateAsync(id, force, allowLlm: llmBudget > 0, ct);
                    llmBudget -= r.LlmCalls;
                    results.Add(r);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogWarning(ex, "[ANALYSIS JOB] {MatchId} üretilemedi", id);
                    results.Add(new AnalysisGenerationResult(id, "Error:" + ex.GetType().Name, null, null, 0, 0, 0));
                }
            }

            _log.LogInformation("[ANALYSIS JOB] {Count} maç: {Summary}", results.Count,
                string.Join(", ", results.GroupBy(r => r.Outcome + "/" + r.Status).Select(g => $"{g.Key}={g.Count()}")));
            return results;
        }
    }
}
