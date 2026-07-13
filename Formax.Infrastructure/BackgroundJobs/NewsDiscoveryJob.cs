using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Discovery;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// FORMAX Data Engine v2 — Global News Discovery scheduler.
    ///
    /// Fixture Discovery'den sonra çalışır. Her 5 dakikalık döngüde:
    ///   • CANLI maçlar (kickoff −3s..+0): her döngü (5 dk).
    ///   • YAKLAŞAN maçlar (kickoff now..+7g): her 15 dk (3 döngüde bir).
    ///   • BİTMİŞ maçlar: işlenmez (haber toplama durur).
    /// Her maç için query üretir → açık provider'ları arar → dedup/cluster/confidence →
    /// MatchNewsArticles'a (FORMAX_MATCH_ID altında) upsert eder.
    /// Mevcut Reasoning/LLM'e dokunmaz.
    /// </summary>
    public sealed class NewsDiscoveryJob : BackgroundService
    {
        private static readonly TimeSpan Cycle = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(40);
        private const int UpcomingEveryNCycles = 3;   // 15 dk
        private const int UpcomingHorizonDays = 7;
        private const int MaxPerCycle = 25;           // HTTP yükünü sınırla

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NewsDiscoveryJob> _logger;
        private int _cycle;

        public NewsDiscoveryJob(IServiceScopeFactory scopeFactory, ILogger<NewsDiscoveryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[NEWS] Discovery scheduler started.");
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[NEWS] Discovery döngüsü başarısız."); }

                _cycle++;
                try { await Task.Delay(Cycle, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var fixtures = scope.ServiceProvider.GetRequiredService<IFixtureRepository>();
            var discovery = scope.ServiceProvider.GetRequiredService<GlobalNewsDiscoveryService>();
            var newsRepo = scope.ServiceProvider.GetRequiredService<IMatchNewsRepository>();
            var intelligence = scope.ServiceProvider.GetRequiredService<MatchIntelligenceService>();
            var evidenceRepo = scope.ServiceProvider.GetRequiredService<IMatchEvidenceRepository>();

            var now = DateTime.UtcNow;
            var includeUpcoming = _cycle % UpcomingEveryNCycles == 0;

            // Canlı (her döngü) + (15 dk'da bir) yaklaşan.
            var live = await fixtures.GetActiveAsync(now.AddHours(-3), now, ct);
            var batch = new List<Fixture>(live);
            if (includeUpcoming)
                batch.AddRange(await fixtures.GetActiveAsync(now, now.AddDays(UpcomingHorizonDays), ct));

            var targets = batch
                .GroupBy(f => f.FormaxMatchId).Select(g => g.First())
                .OrderBy(f => f.KickoffUtc)
                .Take(MaxPerCycle)
                .ToList();

            if (targets.Count == 0) return;

            var totalAdded = 0;
            var totalEvidence = 0;
            foreach (var f in targets)
            {
                ct.ThrowIfCancellationRequested();
                var (items, _) = await discovery.DiscoverForMatchAsync(
                    f.FormaxMatchId, f.HomeTeam, f.AwayTeam, f.League, f.Country, f.KickoffUtc, ct);
                totalAdded += await newsRepo.UpsertAsync(f.FormaxMatchId, items, ct);

                // v2.1 — haberden sinyal/Evidence üret ve Evidence Store'a yaz.
                var evidence = intelligence.BuildEvidence(f.FormaxMatchId, items);
                totalEvidence += await evidenceRepo.UpsertAsync(f.FormaxMatchId, evidence, ct);
            }

            _logger.LogInformation("[NEWS] Cycle — {Matches} maç, {Added} yeni haber, {Evidence} yeni kanıt.",
                targets.Count, totalAdded, totalEvidence);
        }
    }
}
