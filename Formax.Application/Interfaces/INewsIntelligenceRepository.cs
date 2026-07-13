using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>Lightweight match reference (id + league) for news linking.</summary>
    public sealed class TeamMatchRef
    {
        public int MatchId { get; init; }
        public string League { get; init; } = string.Empty;
    }

    /// <summary>
    /// Radar News Intelligence (R.10.1) — persistence for news snapshots plus the
    /// team→matches lookup used to link news to matches.
    /// </summary>
    public interface INewsIntelligenceRepository
    {
        Task<NewsIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default);

        Task UpsertAsync(NewsIntelligenceSnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);

        /// <summary>Matches (id + league) a team plays in, at/after the cutoff.</summary>
        Task<IReadOnlyList<TeamMatchRef>> GetMatchesByTeamFromAsync(
            int teamId, DateTime fromUtc, CancellationToken ct = default);
    }
}
