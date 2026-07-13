using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — persistence for intelligence snapshots plus
    /// the read access needed to assemble a match profile (match + team facts).
    /// </summary>
    public interface IMatchIntelligenceRepository
    {
        Task<MatchIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default);

        Task UpsertAsync(MatchIntelligenceSnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);

        /// <summary>Load a match with both teams (for profile building), or null.</summary>
        Task<Match?> GetMatchWithTeamsAsync(int matchId, CancellationToken ct = default);

        /// <summary>Ids of matches at/after the cutoff (upcoming + recently started).</summary>
        Task<IReadOnlyList<int>> GetMatchIdsFromAsync(DateTime fromUtc, CancellationToken ct = default);

        /// <summary>Most recent finished matches for a team before the cutoff (form).</summary>
        Task<IReadOnlyList<Match>> GetRecentFinishedByTeamAsync(
            int teamId, DateTime beforeUtc, int take, CancellationToken ct = default);

        /// <summary>Finished head-to-head matches between two teams before the cutoff.</summary>
        Task<IReadOnlyList<Match>> GetH2HFinishedAsync(
            int teamAId, int teamBId, DateTime beforeUtc, int take, CancellationToken ct = default);
    }
}
