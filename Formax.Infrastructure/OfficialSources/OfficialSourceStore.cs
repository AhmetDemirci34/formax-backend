using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>
    /// RESMÎ KAYNAK DEFTERİ + KALICI ÖNBELLEK — DB üzerinde, restart-safe.
    /// Fetcher bu depo üzerinden okur/yazar; kullanıcı istek yolu bu sınıfı kullanmaz.
    /// </summary>
    public sealed class OfficialSourceStore
    {
        private readonly FormaxDbContext _db;

        public OfficialSourceStore(FormaxDbContext db) => _db = db;

        public Task<OfficialSourceCacheEntry?> GetCacheAsync(string urlHash, CancellationToken ct)
            => _db.OfficialSourceCache.AsNoTracking().FirstOrDefaultAsync(x => x.UrlHash == urlHash, ct);

        public async Task SaveCacheAsync(OfficialSourceCacheEntry entry, CancellationToken ct)
        {
            var existing = await _db.OfficialSourceCache.FirstOrDefaultAsync(x => x.UrlHash == entry.UrlHash, ct);
            if (existing == null)
            {
                _db.OfficialSourceCache.Add(entry);
            }
            else
            {
                existing.Url = entry.Url;
                existing.SourceKey = entry.SourceKey;
                existing.ETag = entry.ETag;
                existing.LastModified = entry.LastModified;
                existing.ContentType = entry.ContentType;
                existing.ContentHash = entry.ContentHash;
                existing.Body = entry.Body;
                existing.FetchedAtUtc = entry.FetchedAtUtc;
                existing.ValidatedAtUtc = entry.ValidatedAtUtc;
                // İşlenmiş özet yalnız işleyen tarafından değiştirilir.
            }
            await _db.SaveChangesAsync(ct);
        }

        public async Task TouchValidatedAsync(string urlHash, DateTime atUtc, CancellationToken ct)
        {
            var existing = await _db.OfficialSourceCache.FirstOrDefaultAsync(x => x.UrlHash == urlHash, ct);
            if (existing == null) return;
            existing.ValidatedAtUtc = atUtc;
            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkProcessedAsync(string urlHash, string contentHash, CancellationToken ct)
        {
            var existing = await _db.OfficialSourceCache.FirstOrDefaultAsync(x => x.UrlHash == urlHash, ct);
            if (existing == null || existing.ContentHash != contentHash) return;
            existing.ProcessedHash = contentHash;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<long> AppendLedgerAsync(OfficialSourceFetch row, CancellationToken ct)
        {
            _db.OfficialSourceFetches.Add(row);
            await _db.SaveChangesAsync(ct);
            return row.Id;
        }

        public async Task UpdateDecisionAsync(long id, int candidates, int accepted, string decision, CancellationToken ct)
        {
            var row = await _db.OfficialSourceFetches.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row == null) return;
            row.CandidateCount = candidates;
            row.AcceptedCount = accepted;
            row.Decision = decision.Length > 400 ? decision[..400] : decision;
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>Bu turda bu adres zaten başarıyla okundu mu? (restart-safe tur tekilliği)</summary>
        public Task<bool> FetchedInRoundAsync(string roundKey, string urlHash, CancellationToken ct)
            => _db.OfficialSourceFetches.AsNoTracking().AnyAsync(x =>
                x.RoundKey == roundKey && x.UrlHash == urlHash &&
                (x.Outcome == "Fetched" || x.Outcome == "NotModified"), ct);

        /// <summary>Host'a en son GERÇEK ağ isteğinin anı — restart sonrası hız sınırı için.</summary>
        public Task<DateTime?> LastNetworkRequestAsync(string host, CancellationToken ct)
            => _db.OfficialSourceFetches.AsNoTracking()
                  // Ağa gerçekten çıkan her istek HTTP durum kodu taşır (304 dahil);
                  // tur hafızasından verilen satırlarda HttpStatus null'dır.
                  .Where(x => x.Host == host && x.HttpStatus != null)
                  .OrderByDescending(x => x.RequestedAtUtc)
                  .Select(x => (DateTime?)x.RequestedAtUtc)
                  .FirstOrDefaultAsync(ct);
    }
}
