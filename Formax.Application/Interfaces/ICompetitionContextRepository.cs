using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ICompetitionContextRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        CompetitionContext? GetByMatchId(int matchId);

        // ── Async writes ───────────────────────────────────────────────────────

        Task UpsertAsync(CompetitionContext context, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
