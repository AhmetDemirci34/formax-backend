using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Phase 6 — takım sezon istatistikleri deposu. PK (LeagueId, SeasonYear, TeamId=external).
    /// Standings repo ile aynı desen: sync okuma + async upsert.
    /// </summary>
    public interface ITeamSeasonStatisticRepository
    {
        /// <summary>Takımın (external id) sezon istatistiği; yoksa null.</summary>
        TeamSeasonStatistic? GetByTeam(int leagueId, int seasonYear, int teamExternalId);

        /// <summary>PK'ya göre ekle/güncelle (in-place). SaveChanges ayrı çağrılır.</summary>
        Task UpsertAsync(TeamSeasonStatistic stat, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
