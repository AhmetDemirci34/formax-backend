using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar (R.9.4) — read-only access to the Source Engine staging table from the
    /// Intelligence side. This is the first live bridge: Intelligence reads staged
    /// items but never writes to or mutates the Source Engine (it does not flip
    /// Processed in this sprint).
    /// </summary>
    public interface IStagedSourceReader
    {
        /// <summary>Pending (unprocessed) staged items resolved to a team.</summary>
        Task<IReadOnlyList<StagedSourceItem>> GetPendingByTeamAsync(
            int teamId, int take, CancellationToken ct = default);

        /// <summary>Pending (unprocessed) staged items of a category.</summary>
        Task<IReadOnlyList<StagedSourceItem>> GetPendingByCategoryAsync(
            SourceCategory category, int take, CancellationToken ct = default);
    }
}
