using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// <see cref="IMatchOddsRepository"/> — EF Core. Market-başına tek satır; yeni okuma aynı
    /// satırı günceller ve önceki değeri PreviousOdd'a taşır.
    /// </summary>
    public sealed class MatchOddsRepository : IMatchOddsRepository
    {
        private readonly FormaxDbContext _db;

        public MatchOddsRepository(FormaxDbContext db) => _db = db;

        public async Task<int> UpsertAsync(
            int matchId,
            IReadOnlyDictionary<string, (decimal Odd, int BookmakerId, string BookmakerName)> markets,
            CancellationToken ct = default)
        {
            if (markets.Count == 0) return 0;

            var existing = await _db.MatchMarketOdds
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            var byKey = existing.ToDictionary(x => x.MarketKey, StringComparer.Ordinal);
            var now = DateTime.UtcNow;
            var touched = 0;

            foreach (var (key, value) in markets)
            {
                if (byKey.TryGetValue(key, out var row))
                {
                    // Oran değişmediyse yalnız zaman damgası tazelenir; PreviousOdd bozulmaz.
                    if (row.Odd != value.Odd)
                    {
                        row.PreviousOdd = row.Odd;
                        row.Odd = value.Odd;
                    }
                    row.BookmakerId = value.BookmakerId;
                    row.BookmakerName = value.BookmakerName;
                    row.CapturedAtUtc = now;
                }
                else
                {
                    _db.MatchMarketOdds.Add(new MatchMarketOdd
                    {
                        MatchId = matchId,
                        MarketKey = key,
                        Odd = value.Odd,
                        BookmakerId = value.BookmakerId,
                        BookmakerName = value.BookmakerName,
                        PreviousOdd = null,
                        CapturedAtUtc = now
                    });
                }
                touched++;
            }

            await _db.SaveChangesAsync(ct);
            return touched;
        }

        public async Task<IReadOnlyList<MatchMarketOdd>> GetByMatchAsync(
            int matchId, CancellationToken ct = default)
            => await _db.MatchMarketOdds.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<MatchMarketOdd>> GetByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default)
        {
            if (matchIds.Count == 0) return Array.Empty<MatchMarketOdd>();
            return await _db.MatchMarketOdds.AsNoTracking()
                .Where(x => matchIds.Contains(x.MatchId))
                .ToListAsync(ct);
        }

        public async Task<int> CountMatchesWithOddsAsync(CancellationToken ct = default)
            => await _db.MatchMarketOdds.AsNoTracking()
                .Select(x => x.MatchId)
                .Distinct()
                .CountAsync(ct);
    }
}
