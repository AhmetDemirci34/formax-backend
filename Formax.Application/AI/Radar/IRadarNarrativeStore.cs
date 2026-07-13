using System;
using System.Collections.Concurrent;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — anlatı snapshot deposu. LLM çıktısı burada cache'lenir;
    /// aynı context için tekrar LLM çağrılmaz (maliyet + latency). Faz 1'de bellek
    /// içi; arayüz sayesinde ileride DB tablosuna (AINarrativeSnapshot) taşınabilir.
    /// </summary>
    public interface IRadarNarrativeStore
    {
        bool TryGet(string key, out RadarNarrativeResult result);
        void Set(string key, RadarNarrativeResult result);
    }

    /// <summary>Bellek içi, TTL'li, dış bağımlılıksız uygulama (singleton).</summary>
    public sealed class InMemoryRadarNarrativeStore : IRadarNarrativeStore
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);
        private readonly ConcurrentDictionary<string, Entry> _cache = new();

        public bool TryGet(string key, out RadarNarrativeResult result)
        {
            if (_cache.TryGetValue(key, out var e) && DateTime.UtcNow - e.StoredAt < Ttl)
            {
                result = e.Result;
                return true;
            }
            result = null!;
            return false;
        }

        public void Set(string key, RadarNarrativeResult result)
            => _cache[key] = new Entry(result, DateTime.UtcNow);

        private readonly record struct Entry(RadarNarrativeResult Result, DateTime StoredAt);
    }
}
