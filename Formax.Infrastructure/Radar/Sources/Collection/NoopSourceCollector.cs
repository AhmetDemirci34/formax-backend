using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Sources.Collection;
using Formax.Domain.Enums;

namespace Formax.Infrastructure.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — dummy collector used to prove the dispatch flow
    /// end to end. Returns an empty successful result; performs NO data collection
    /// (no HTTP, no RSS, no TheSportsDB, no scrape). One instance is registered per
    /// configured <see cref="SourceType"/>; real collectors replace these in R.8.x.
    /// </summary>
    public sealed class NoopSourceCollector : SourceCollectorBase
    {
        public override SourceType SupportedType { get; }

        public NoopSourceCollector(SourceType supportedType)
        {
            SupportedType = supportedType;
        }

        protected override Task<IReadOnlyList<SourceCollectorItem>> CollectCoreAsync(
            SourceCollectorContext context, CancellationToken ct)
        {
            // Intentionally empty — flow verification only.
            IReadOnlyList<SourceCollectorItem> empty = Array.Empty<SourceCollectorItem>();
            return Task.FromResult(empty);
        }
    }
}
