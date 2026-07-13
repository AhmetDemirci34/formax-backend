using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — default registry implementation.
    /// Pure read/query layer over the definition + status repositories. Holds no
    /// state and performs no collection; later engine layers depend on this contract.
    /// </summary>
    public sealed class SourceRegistry : ISourceRegistry
    {
        private readonly ISourceDefinitionRepository _definitions;
        private readonly ISourceStatusRepository _statuses;

        public SourceRegistry(
            ISourceDefinitionRepository definitions,
            ISourceStatusRepository statuses)
        {
            _definitions = definitions;
            _statuses = statuses;
        }

        public Task<IReadOnlyList<SourceDefinition>> GetDefinitionsAsync(CancellationToken ct = default)
            => _definitions.GetAllAsync(ct);

        public async Task<IReadOnlyList<SourceDefinition>> GetUsableAsync(CancellationToken ct = default)
        {
            var defs = await _definitions.GetAllAsync(ct);
            var statuses = await _statuses.GetAllAsync(ct);
            var activeById = statuses.ToDictionary(s => s.SourceId, s => s.Active);

            return defs
                .Where(d => d.Enabled
                            && (!activeById.TryGetValue(d.Id, out var active) || active))
                .OrderBy(d => d.FailoverGroup)
                .ThenBy(d => d.Priority)
                .ToList();
        }

        public async Task<IReadOnlyList<SourceDefinition>> GetFailoverGroupAsync(
            string failoverGroup, CancellationToken ct = default)
        {
            var usable = await GetUsableAsync(ct);
            return usable
                .Where(d => d.FailoverGroup == failoverGroup)
                .OrderBy(d => d.Priority)
                .ToList();
        }

        public Task<SourceStatus?> GetStatusAsync(int sourceId, CancellationToken ct = default)
            => _statuses.GetBySourceIdAsync(sourceId, ct);
    }
}
