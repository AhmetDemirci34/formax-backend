using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchSapmaSnapshotRepository
    {
        Task UpsertAsync(MatchSapmaSnapshot snapshot);
        Task SaveChangesAsync();

        // Home vitrini için: verilen maç id'lerine ait "taze" snapshotları getir.
        Task<IReadOnlyList<MatchSapmaSnapshot>> GetFreshByMatchIdsAsync(IReadOnlyList<int> matchIds, DateTime utcNow);
    }
}