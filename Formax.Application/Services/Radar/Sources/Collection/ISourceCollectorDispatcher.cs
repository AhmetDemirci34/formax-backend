using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — the scheduler's single entry point into the
    /// collector layer. Resolves the collector via the factory, builds the context,
    /// and runs it. Never throws (no collector found is a failed result).
    /// </summary>
    public interface ISourceCollectorDispatcher
    {
        Task<SourceCollectorResult> DispatchAsync(
            SourceDefinition definition, DateTime nowUtc, CancellationToken ct = default);
    }
}
