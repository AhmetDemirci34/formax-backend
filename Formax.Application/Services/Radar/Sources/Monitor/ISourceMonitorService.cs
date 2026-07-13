using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources.Monitor
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — reads a source's health snapshot (plus staging
    /// signal) and produces an autonomous monitor verdict. Called by the scheduler
    /// after health is recorded. Evaluation only — no notifications, no failover.
    /// </summary>
    public interface ISourceMonitorService
    {
        Task<MonitorEvaluationResult> EvaluateAsync(
            SourceDefinition definition, DateTime nowUtc, CancellationToken ct = default);

        Task<SourceMonitorSnapshot?> GetAsync(int sourceId, CancellationToken ct = default);
    }
}
