using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>
    /// SONUÇ SONRASI ÖNBELLEK TEMİZLİĞİ — resmî sonuç kanonik DB'ye yazılınca süreç içi önbellekte bayat kalan girdiler silinir.
    ///
    /// Maç detayı, Sonuçlar listesi ve AI BEKLENTİSİ snapshot'ı her istekte DB'den okunur (önbelleksiz); bayat kalabilen tek yer
    /// Keşfet karar paketi önbelleğidir (<c>decision:pkg:{matchId}</c>, 10+ dk). Biten maçın kendi paketi ve iki takımın yaklaşan
    /// maçlarının paketleri (form/H2H girdisi değişti) silinir; bir sonraki okuma güncel DB'den kurar. Hesap değişmez.
    /// </summary>
    public sealed class MatchResultCacheInvalidator
    {
        /// <summary>Takımların bu pencere içindeki yaklaşan maçlarının paketleri temizlenir.</summary>
        public static readonly TimeSpan UpcomingWindow = TimeSpan.FromDays(30);

        private readonly IMemoryCache _cache;

        public MatchResultCacheInvalidator(IMemoryCache cache) => _cache = cache;

        public async Task<IReadOnlyList<int>> InvalidateAsync(FormaxDbContext db, int matchId, int homeTeamId, int awayTeamId,
            DateTime nowUtc, CancellationToken ct = default)
        {
            var until = nowUtc + UpcomingWindow;
            var related = await db.Matches.AsNoTracking()
                .Where(m => m.Id != matchId && m.MatchDate >= nowUtc && m.MatchDate <= until
                            && m.Status != MatchStatuses.Finished
                            && (m.HomeTeamId == homeTeamId || m.AwayTeamId == homeTeamId
                                || m.HomeTeamId == awayTeamId || m.AwayTeamId == awayTeamId))
                .Select(m => m.Id).Take(200)
                .ToListAsync(ct).ConfigureAwait(false);

            var evicted = new List<int>(related.Count + 1) { matchId };
            evicted.AddRange(related);
            foreach (var id in evicted) _cache.Remove(global::GetRecommendationFeedUseCase.DecisionPackageCacheKey(id));
            return evicted;
        }
    }
}
