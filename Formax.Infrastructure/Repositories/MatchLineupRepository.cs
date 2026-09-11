using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchLineupRepository : IMatchLineupRepository
    {
        private readonly FormaxDbContext _context;

        public MatchLineupRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        public MatchLineup? GetByMatchId(int matchId)
            => _context.MatchLineups.FirstOrDefault(x => x.MatchId == matchId);

        public List<MatchLineupPlayer> GetPlayersByMatchId(int matchId)
            => _context.MatchLineupPlayers
                .Where(x => x.MatchId == matchId)
                .ToList();

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task UpsertAsync(MatchLineup lineup, CancellationToken ct = default)
        {
            var existing = await _context.MatchLineups
                .FirstOrDefaultAsync(x => x.MatchId == lineup.MatchId, ct);

            if (existing == null)
            {
                _context.MatchLineups.Add(lineup);
            }
            else
            {
                existing.HomeLineupsReleased = lineup.HomeLineupsReleased;
                existing.AwayLineupsReleased = lineup.AwayLineupsReleased;
                // Diziliş her fetch'te tazelenir (teknik direktör açıklamayı değiştirebilir).
                existing.HomeFormation = lineup.HomeFormation;
                existing.AwayFormation = lineup.AwayFormation;
                existing.ReleasedAt ??= lineup.ReleasedAt;   // only set first time
                existing.FetchedAt = lineup.FetchedAt;
                existing.ExternalFixtureId = lineup.ExternalFixtureId ?? existing.ExternalFixtureId;
                existing.HomeTeamExternalId = lineup.HomeTeamExternalId ?? existing.HomeTeamExternalId;
                existing.AwayTeamExternalId = lineup.AwayTeamExternalId ?? existing.AwayTeamExternalId;
                existing.HomeCoach = lineup.HomeCoach ?? existing.HomeCoach;
                existing.AwayCoach = lineup.AwayCoach ?? existing.AwayCoach;
                existing.Provider = lineup.Provider ?? existing.Provider;
                existing.LastCheckedAtUtc = lineup.LastCheckedAtUtc ?? existing.LastCheckedAtUtc;
                _context.MatchLineups.Update(existing);
            }
        }

        public async Task MarkCheckedAsync(int matchId, string? externalFixtureId, string provider,
            DateTime checkedAtUtc, CancellationToken ct = default)
        {
            var existing = await _context.MatchLineups
                .FirstOrDefaultAsync(x => x.MatchId == matchId, ct);

            if (existing == null)
            {
                _context.MatchLineups.Add(new MatchLineup
                {
                    MatchId = matchId,
                    HomeLineupsReleased = false,
                    AwayLineupsReleased = false,
                    FetchedAt = checkedAtUtc,
                    ExternalFixtureId = externalFixtureId,
                    Provider = provider,
                    LastCheckedAtUtc = checkedAtUtc
                });
                return;
            }

            // Kadro/oyuncu/diziliş DEĞİŞMEZ — boş cevap var olan veriyi silmez.
            existing.LastCheckedAtUtc = checkedAtUtc;
            existing.ExternalFixtureId ??= externalFixtureId;
            existing.Provider ??= provider;
        }

        public async Task ReplacePlayersAsync(
            int matchId,
            IEnumerable<MatchLineupPlayer> players,
            CancellationToken ct = default)
        {
            var existing = await _context.MatchLineupPlayers
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            _context.MatchLineupPlayers.RemoveRange(existing);

            foreach (var p in players)
                _context.MatchLineupPlayers.Add(p);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
