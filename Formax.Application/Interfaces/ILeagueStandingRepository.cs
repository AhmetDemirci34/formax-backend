using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ILeagueStandingRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        List<LeagueStanding> GetByLeague(int leagueId, int seasonYear);

        LeagueStanding? GetByTeam(int leagueId, int seasonYear, int teamId);

        /// <summary>
        /// Takımın ULUSAL LİG kimliği — puan durumu tablosu yalnız ligler için yazılır
        /// (kupa/Avrupa turnuvalarının LeagueStandings satırı YOKTUR, ölçüldü: 2026 sezonunda
        /// yalnız 39/40/61/78/88/135/140/203 dolu). Bu yüzden "bu takımın ligi" sorusunun
        /// gerçek veriye dayalı cevabı budur — isim tahmini veya sabit liste kullanılmaz.
        ///
        /// LeagueStandings.TeamId sağlayıcının EXTERNAL id'sini tutar; canonical id ile
        /// eşleşmediği için çağıran her iki adayı da geçebilir. Bulunamazsa null.
        /// </summary>
        int? FindLeagueIdForTeam(int seasonYear, IEnumerable<int> candidateTeamIds);

        // ── Async writes ───────────────────────────────────────────────────────

        /// <summary>
        /// Deletes existing rows for (leagueId, seasonYear) then inserts the new set.
        /// </summary>
        Task ReplaceAsync(int leagueId, int seasonYear, IEnumerable<LeagueStanding> standings, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
