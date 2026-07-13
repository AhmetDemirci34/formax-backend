using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — base class that enforces the collector contract:
    /// times the run, catches everything (including <see cref="CollectorException"/>),
    /// and converts failures into a failed <see cref="SourceCollectorResult"/> so the
    /// dispatcher never sees an exception. Subclasses implement only
    /// <see cref="CollectCoreAsync"/>.
    /// </summary>
    public abstract class SourceCollectorBase : ISourceCollector
    {
        public abstract SourceType SupportedType { get; }

        public async Task<SourceCollectorResult> CollectAsync(
            SourceCollectorContext context, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var startedAt = DateTime.UtcNow;

            try
            {
                var items = await CollectCoreAsync(context, ct);
                sw.Stop();
                return SourceCollectorResult.Ok(
                    context.SourceKey, items, sw.Elapsed.TotalMilliseconds, startedAt);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                return SourceCollectorResult.Fail(
                    context.SourceKey, "cancelled", sw.Elapsed.TotalMilliseconds, startedAt);
            }
            catch (CollectorException cex)
            {
                sw.Stop();
                return SourceCollectorResult.Fail(
                    context.SourceKey, cex.Message, sw.Elapsed.TotalMilliseconds, startedAt);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return SourceCollectorResult.Fail(
                    context.SourceKey, $"unhandled: {ex.Message}", sw.Elapsed.TotalMilliseconds, startedAt);
            }
        }

        /// <summary>
        /// Core collection logic. May throw <see cref="CollectorException"/> on failure;
        /// the base converts it. Must return raw items only — no normalization, no retry.
        /// </summary>
        protected abstract Task<System.Collections.Generic.IReadOnlyList<SourceCollectorItem>>
            CollectCoreAsync(SourceCollectorContext context, CancellationToken ct);
    }
}
