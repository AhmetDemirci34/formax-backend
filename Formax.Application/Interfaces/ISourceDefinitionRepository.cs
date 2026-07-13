using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — persistence contract for source definitions.
    /// </summary>
    public interface ISourceDefinitionRepository
    {
        Task<IReadOnlyList<SourceDefinition>> GetAllAsync(CancellationToken ct = default);

        Task<SourceDefinition?> GetByKeyAsync(string sourceKey, CancellationToken ct = default);

        /// <summary>Insert or update by <see cref="SourceDefinition.SourceKey"/>.
        /// Returns the persisted entity (with Id populated).</summary>
        Task<SourceDefinition> UpsertAsync(SourceDefinition definition, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
