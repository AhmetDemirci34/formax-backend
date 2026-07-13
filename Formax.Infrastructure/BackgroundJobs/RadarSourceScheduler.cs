using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Radar.Sources.Collection;
using Formax.Application.Services.Radar.Sources.Health;
using Formax.Application.Services.Radar.Sources.Monitor;
using Formax.Application.Services.Radar.Sources.Normalization;
using Formax.Application.Services.Radar.Sources.Staging;
using Formax.Application.Services.Radar.Sources.Scheduling;
using Formax.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Formax.Infrastructure.Radar.Sources.Scheduling;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — the single orchestrator job.
    ///
    /// Each cycle it reads usable sources from the registry, asks the pure planner
    /// which are due (category interval + per-source jitter, skipping circuit-open
    /// sources), then "dispatches" the due ones under a per-source execution lock.
    ///
    /// THIS SPRINT DOES NOT COLLECT DATA. Dispatch is a no-op marker: the slot where
    /// a collector call will go (R.8.3+) only records the run via the schedule tracker.
    /// No RSS read, no TheSportsDB call, no normalizer, no staging.
    /// </summary>
    public sealed class RadarSourceScheduler : BackgroundService
    {
        private readonly ILogger<RadarSourceScheduler> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISourceSchedulePlanner _planner;
        private readonly ISourceExecutionLock _executionLock;
        private readonly ISourceScheduleTracker _tracker;
        private readonly SchedulerOptions _options;

        public RadarSourceScheduler(
            ILogger<RadarSourceScheduler> logger,
            IServiceScopeFactory scopeFactory,
            ISourceSchedulePlanner planner,
            ISourceExecutionLock executionLock,
            ISourceScheduleTracker tracker,
            IOptions<SchedulerOptions> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _planner = planner;
            _executionLock = executionLock;
            _tracker = tracker;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[RADAR SCHEDULER] started (plan-only, no collection).");

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);

            var loopDelay = TimeSpan.FromSeconds(Math.Max(5, _options.LoopDelaySeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RADAR SCHEDULER] cycle error");
                }

                await Task.Delay(loopDelay, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<ISourceRegistry>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ISourceCollectorDispatcher>();
            var normalizerDispatcher = scope.ServiceProvider.GetRequiredService<INormalizerDispatcher>();
            var stagingDispatcher = scope.ServiceProvider.GetRequiredService<IStagingDispatcher>();
            var healthService = scope.ServiceProvider.GetRequiredService<ISourceHealthService>();
            var monitorService = scope.ServiceProvider.GetRequiredService<ISourceMonitorService>();

            var usable = await registry.GetUsableAsync(ct);
            if (usable.Count == 0)
            {
                _logger.LogDebug("[RADAR SCHEDULER] no usable sources.");
                return;
            }

            var now = DateTime.UtcNow;

            // Assemble planner input (circuit state + last run) — registry side, kept
            // out of the pure planner.
            var contexts = new List<SourceScheduleContext>(usable.Count);
            foreach (var def in usable)
            {
                var status = await registry.GetStatusAsync(def.Id, ct);
                contexts.Add(new SourceScheduleContext
                {
                    Definition = def,
                    Circuit = status?.CircuitState ?? SourceCircuitState.Closed,
                    LastRunUtc = _tracker.GetLastRun(def.SourceKey)
                });
            }

            var plan = _planner.Plan(contexts, BuildCategoryIntervals(), Jitter(), now);

            var definitionsByKey = contexts.ToDictionary(c => c.Definition.SourceKey, c => c.Definition);

            int dispatched = 0, locked = 0;

            foreach (var result in plan.Results)
            {
                if (result.Outcome != SourceExecutionOutcome.Due)
                    continue;

                if (!_executionLock.TryAcquire(result.SourceKey))
                {
                    locked++;
                    _logger.LogDebug("[RADAR SCHEDULER] {Key} skipped — already locked.", result.SourceKey);
                    continue;
                }

                try
                {
                    // ── Collector → Normalizer → Staging (R.8.5). Dummy collectors
                    //    return empty results, so the chain runs end to end but stages
                    //    zero rows. No HTTP/RSS/TheSportsDB; only the staging table is
                    //    written (when there is data). ──
                    var definition = definitionsByKey[result.SourceKey];

                    var collectResult = await dispatcher.DispatchAsync(definition, now, ct);
                    var normResult = normalizerDispatcher.Dispatch(definition, collectResult, now);
                    var stageResult = await stagingDispatcher.DispatchAsync(definition, normResult, ct);

                    // ── Health (R.8.6). Execution is "successful" when the whole
                    //    collect→normalize→stage chain did not fail. Measurement only. ──
                    var executionOk = collectResult.Success
                        && stageResult.Outcome != StagingWriteOutcome.Failed;
                    var healthResult = await healthService.RecordExecutionAsync(
                        definition, executionOk, collectResult.DurationMs, now, ct);

                    // ── Monitor (R.8.7). Reads the health snapshot + staging signal
                    //    and records an autonomous verdict. Evaluation only — no
                    //    notifications, no failover. ──
                    var monitorResult = await monitorService.EvaluateAsync(definition, now, ct);

                    _tracker.SetLastRun(result.SourceKey, now);
                    dispatched++;

                    _logger.LogInformation(
                        "[RADAR SCHEDULER] DUE {Key} (cat={Cat}, group={Group}) — {Reason} → collect(success={Success},items={Items}) → normalize(canon={Canon},unres={Unres}) → stage(outcome={Outcome},written={Written}) → health(score={Score:F0},status={HStatus}) → monitor(status={MStatus},alert={Alert})",
                        result.SourceKey, result.Category, result.FailoverGroup, result.Reason,
                        collectResult.Success, collectResult.ItemCount,
                        normResult.NormalizedCount, normResult.UnresolvedCount,
                        stageResult.Outcome, stageResult.WrittenCount,
                        healthResult.HealthScore, healthResult.Status,
                        monitorResult.Status, monitorResult.AlertType);
                }
                finally
                {
                    _executionLock.Release(result.SourceKey);
                }
            }

            var notDue = plan.Results.Count(r => r.Outcome == SourceExecutionOutcome.NotDue);
            var circuit = plan.Results.Count(r => r.Outcome == SourceExecutionOutcome.SkippedCircuitOpen);

            _logger.LogInformation(
                "[RADAR SCHEDULER] cycle done: usable={Usable} dispatched={Dispatched} notDue={NotDue} circuitOpen={Circuit} locked={Locked}",
                usable.Count, dispatched, notDue, circuit, locked);
        }

        private IReadOnlyDictionary<SourceCategory, TimeSpan> BuildCategoryIntervals()
            => new Dictionary<SourceCategory, TimeSpan>
            {
                [SourceCategory.Match] = TimeSpan.FromSeconds(Math.Max(1, _options.MatchIntervalSeconds)),
                [SourceCategory.News] = TimeSpan.FromSeconds(Math.Max(1, _options.NewsIntervalSeconds)),
                [SourceCategory.Behavior] = TimeSpan.FromSeconds(Math.Max(1, _options.BehaviorIntervalSeconds)),
                [SourceCategory.Odds] = TimeSpan.FromSeconds(Math.Max(1, _options.OddsIntervalSeconds)),
            };

        private TimeSpan Jitter() => TimeSpan.FromSeconds(Math.Max(0, _options.JitterSeconds));
    }
}
