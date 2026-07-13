using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources.Health
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — records execution outcomes and maintains the
    /// per-source health snapshot. Called by the scheduler after each dispatch.
    /// Measurement only — it does not act on the health (no failover, no alerts).
    /// </summary>
    public interface ISourceHealthService
    {
        /// <summary>
        /// Record one execution outcome for a source and recompute its health.
        /// </summary>
        Task<HealthCalculationResult> RecordExecutionAsync(
            SourceDefinition definition,
            bool success,
            double executionMs,
            DateTime nowUtc,
            CancellationToken ct = default);

        Task<SourceHealthSnapshot?> GetAsync(int sourceId, CancellationToken ct = default);
    }
}
