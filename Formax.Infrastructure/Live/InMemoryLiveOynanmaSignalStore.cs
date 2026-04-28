using System.Collections.Concurrent;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Live
{
    /// <summary>
    /// In-memory canlı oynanma store.
    /// UYARI: Uygulama restart olunca veri gider.
    /// Amaç: Canlı oynanma entegrasyonu gelmeden önce uçtan uca testi mümkün kılmak.
    /// </summary>
    public sealed class InMemoryLiveOynanmaSignalStore : ILiveOynanmaSignalStore
    {
        private readonly ConcurrentDictionary<int, OynanmaSinyalleri> _cache = new();

        public Task<OynanmaSinyalleri?> GetAsync(int matchId)
        {
            return Task.FromResult(_cache.TryGetValue(matchId, out var v) ? v : null);
        }

        public Task UpsertAsync(int matchId, OynanmaSinyalleri sinyaller)
        {
            // her upsert'te tazelik zamanı güncellensin
            sinyaller.LastUpdatedAtUtc = System.DateTime.UtcNow;
            _cache[matchId] = sinyaller;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(int matchId)
        {
            var ok = _cache.TryRemove(matchId, out _);
            return Task.FromResult(ok);
        }
    }
}
