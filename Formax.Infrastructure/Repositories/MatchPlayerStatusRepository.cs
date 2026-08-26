using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchPlayerStatusRepository : IMatchPlayerStatusRepository
    {
        private readonly FormaxDbContext _context;

        public MatchPlayerStatusRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        /// <summary>
        /// Bir maçın oyuncu durumları — OYUNCU BAZINDA TEKİL.
        ///
        /// Sağlayıcı aynı oyuncu için birden çok satır döndürebiliyordu ve ingestion tarafında
        /// tekilleştirme yoktu; depoda bu şekilde yazılmış kayıtlar bulunuyor (ölçüm: 300 satır /
        /// 150 tekil oyuncu — çiftler alan alan aynı). Tekilleştirilmeden okunursa
        /// <c>BuildAvailability</c> eksik oyuncuyu iki kez sayar ve beklenen gol cezası ile
        /// 1X2 edge düzeltmesi iki katına çıkar. Ingestion artık tekilleştiriyor; burası
        /// DEPODA DURAN eski kayıtları da güvenli hâle getirir (DB'ye yazma yapılmaz).
        ///
        /// Aynı oyuncunun birden çok kaydında EN AĞIR durum kazanır (Suspended > Injured >
        /// Doubtful) → kesin eksik, "Doubtful" bir kopya yüzünden kaybolmaz.
        /// </summary>
        public List<MatchPlayerStatus> GetByMatchId(int matchId)
        {
            var rows = _context.MatchPlayerStatuses
                .Where(x => x.MatchId == matchId)
                .ToList();

            static int Severity(string? status) => status switch
            {
                "Suspended" => 3,
                "Injured" => 2,
                _ => 1
            };

            return rows
                .GroupBy(x => $"{x.TeamId}|{(x.PlayerName ?? string.Empty).Trim().ToLowerInvariant()}")
                .Select(g => g.OrderByDescending(x => Severity(x.Status))
                              .ThenByDescending(x => x.FetchedAt)
                              .First())
                .ToList();
        }

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task ReplaceAsync(
            int matchId,
            IEnumerable<MatchPlayerStatus> statuses,
            CancellationToken ct = default)
        {
            var existing = await _context.MatchPlayerStatuses
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            _context.MatchPlayerStatuses.RemoveRange(existing);

            foreach (var s in statuses)
                _context.MatchPlayerStatuses.Add(s);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
