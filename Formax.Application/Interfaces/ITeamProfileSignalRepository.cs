using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Phase 6 Final — takım profili deposu (coach/venue/squad/transfers). PK = internal Team.Id.
    /// TeamSeasonStatistic repo deseni: sync okuma + async upsert.
    /// </summary>
    public interface ITeamProfileSignalRepository
    {
        /// <summary>Takımın (internal id) profil sinyali; yoksa null.</summary>
        TeamProfileSignal? GetByTeam(int teamId);

        /// <summary>Team.Id'ye göre ekle/güncelle (in-place). SaveChanges ayrı çağrılır.</summary>
        Task UpsertAsync(TeamProfileSignal signal, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
