using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchLineupRepository
    {
        // ── Reads (synchronous — used from synchronous use cases) ──────────

        MatchLineup? GetByMatchId(int matchId);

        List<MatchLineupPlayer> GetPlayersByMatchId(int matchId);

        // ── Writes (async — used from background jobs) ─────────────────────

        Task UpsertAsync(MatchLineup lineup, CancellationToken ct = default);

        /// <summary>
        /// Deletes existing players for the match then inserts the new set.
        /// </summary>
        Task ReplacePlayersAsync(int matchId, IEnumerable<MatchLineupPlayer> players, CancellationToken ct = default);

        /// <summary>
        /// Sağlayıcı GEÇERLİ ama kadrosuz cevap verdi: yalnız son kontrol anı yazılır.
        /// Başlık yoksa "yayımlanmadı" başlığı açılır; var olan kadro/oyuncular SİLİNMEZ.
        /// </summary>
        Task MarkCheckedAsync(int matchId, string? externalFixtureId, string provider,
            DateTime checkedAtUtc, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
