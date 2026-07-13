using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Sources.Normalization;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources.Staging
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — final seam of the data path. Takes a
    /// normalization result and persists its canonical items into staging. This is the
    /// only Source Engine component that writes to the database (its own staging
    /// table); it never touches Match/Team or starts Intelligence.
    /// </summary>
    public interface IStagingDispatcher
    {
        Task<StagingWriteResult> DispatchAsync(
            SourceDefinition definition,
            SourceNormalizationResult normalizationResult,
            CancellationToken ct = default);
    }
}
