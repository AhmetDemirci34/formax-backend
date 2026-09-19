using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Lineups;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Lineups
{
    /// <summary>
    /// KADRO GÖZLEM YÜKLEYİCİSİ — canonical kadro satırlarını (MatchLineups + MatchLineupPlayers)
    /// oyuncu etki modelinin okuyabileceği saf gözlemlere çevirir.
    ///
    /// DIŞ İSTEK YOKTUR: yalnız DB okur. Oyuncu kimliği (takım, normalleştirilmiş ad) çiftidir;
    /// ad çözülemeyen satır <c>unresolved</c> yazılır ve etki hesabına GİRMEZ (yanlış oyuncuyla
    /// birleştirilmez).
    /// </summary>
    public sealed class LineupHistoryLoader
    {
        private readonly FormaxDbContext _db;
        public LineupHistoryLoader(FormaxDbContext db) => _db = db;

        /// <summary>
        /// Verilen maçların kadro gözlemleri. <paramref name="officialOnly"/> true iken yalnız resmî
        /// kaynaktan doğrulanmış kadrolar döner (üretim yolu); ölçüm yolunda kapsamı görebilmek için
        /// false verilebilir — bu durumda kaynak damgası gözlemde taşınır ve rapor ayrı sayar.
        /// </summary>
        public async Task<List<MatchLineupObservation>> LoadAsync(
            IReadOnlyCollection<int>? matchIds, bool officialOnly, CancellationToken ct = default)
        {
            var headerQuery = _db.MatchLineups.AsNoTracking()
                .Where(l => l.HomeLineupsReleased || l.AwayLineupsReleased);
            if (officialOnly)
                headerQuery = headerQuery.Where(l => l.Provider != null && l.Provider.StartsWith("official:"));
            if (matchIds != null)
                headerQuery = headerQuery.Where(l => matchIds.Contains(l.MatchId));

            var headers = await headerQuery
                .Select(l => new { l.MatchId, l.VerificationStatus, l.SourceKey, l.VerifiedAtUtc, l.Provider })
                .ToListAsync(ct).ConfigureAwait(false);
            if (headers.Count == 0) return new List<MatchLineupObservation>();

            var ids = headers.Select(h => h.MatchId).ToList();
            var matches = await _db.Matches.AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.MatchDate, m.LeagueId, m.HomeTeamId, m.AwayTeamId })
                .ToDictionaryAsync(m => m.Id, ct).ConfigureAwait(false);

            var players = (await _db.MatchLineupPlayers.AsNoTracking()
                    .Where(p => ids.Contains(p.MatchId))
                    .Select(p => new { p.MatchId, p.Side, p.Role, p.ShirtNumber, p.PlayerName, p.Position })
                    .ToListAsync(ct).ConfigureAwait(false))
                .GroupBy(p => p.MatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var list = new List<MatchLineupObservation>(headers.Count);
            foreach (var h in headers)
            {
                if (!matches.TryGetValue(h.MatchId, out var m)) continue;
                var rows = players.GetValueOrDefault(h.MatchId) ?? new();

                List<LineupPlayerObservation> Side(string side, int teamId) => rows
                    .Where(p => string.Equals(p.Side, side, StringComparison.OrdinalIgnoreCase))
                    .Select(p => new LineupPlayerObservation(
                        PlayerIdentity.Key(teamId, p.PlayerName),
                        p.PlayerName,
                        p.Position,
                        p.ShirtNumber > 0 ? p.ShirtNumber : null,
                        string.Equals(p.Role, "Starter", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                list.Add(new MatchLineupObservation(
                    m.Id,
                    DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc),
                    m.LeagueId,
                    m.HomeTeamId,
                    m.AwayTeamId,
                    Side("Home", m.HomeTeamId),
                    Side("Away", m.AwayTeamId),
                    h.VerificationStatus ?? "Pending",
                    h.SourceKey,
                    h.VerifiedAtUtc));
            }
            return list.OrderBy(l => l.KickoffUtc).ThenBy(l => l.MatchId).ToList();
        }

        /// <summary>Tek maçın kadro gözlemi (snapshot yolu).</summary>
        public async Task<MatchLineupObservation?> LoadOneAsync(int matchId, CancellationToken ct = default)
            => (await LoadAsync(new[] { matchId }, officialOnly: true, ct).ConfigureAwait(false)).FirstOrDefault();
    }
}
