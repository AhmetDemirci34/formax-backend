using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>Feed montajının ihtiyaç duyduğu tek şey — intelligence snapshot'ın iki alanı.</summary>
    public sealed record MatchIntelligenceFeedRow(int MatchId, double ImportanceScore, string PrimarySignal);

    /// <summary>
    /// Radar Match Intelligence (R.9.1) — persistence for intelligence snapshots plus
    /// the read access needed to assemble a match profile (match + team facts).
    /// </summary>
    public interface IMatchIntelligenceRepository
    {
        Task<MatchIntelligenceSnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default);

        /// <summary>
        /// Batch read (perf): snapshots for the given match ids in a SINGLE query.
        /// Feed/recommendation yollarında maç-başına GetByMatchIdAsync yerine kullanılır —
        /// dönen veri birebir aynı, yalnız round-trip sayısı N → 1 olur.
        /// </summary>
        Task<IReadOnlyDictionary<int, MatchIntelligenceSnapshot>> GetByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default);

        /// <summary>
        /// Feed kurulumu için hafif projeksiyon: yalnız ImportanceScore + PrimarySignalType.
        /// Tam entity, büyük SignalsJson metnini de taşır; feed bu alanı KULLANMAZ. Binlerce
        /// satırda bu kolonu çekmemek okuma süresini belirgin düşürür. Değerler aynıdır.
        /// </summary>
        Task<IReadOnlyDictionary<int, MatchIntelligenceFeedRow>> GetFeedRowsFromAsync(
            DateTime fromUtc, CancellationToken ct = default);

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
