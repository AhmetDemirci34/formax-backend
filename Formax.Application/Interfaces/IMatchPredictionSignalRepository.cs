using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Phase 6 / Slice 2 — maç öngörü sinyali deposu (AI-only). PK = MatchId.
    /// CompetitionContext repo deseni: sync okuma + async upsert.
    /// </summary>
    public interface IMatchPredictionSignalRepository
    {
        /// <summary>Maçın öngörü sinyali; yoksa null.</summary>
        MatchPredictionSignal? GetByMatchId(int matchId);

        /// <summary>MatchId'ye göre ekle/güncelle (in-place). SaveChanges ayrı çağrılır.</summary>
        Task UpsertAsync(MatchPredictionSignal signal, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
