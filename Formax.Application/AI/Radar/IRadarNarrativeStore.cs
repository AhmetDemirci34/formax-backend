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

        /// <summary>
        /// HAZIR SNAPSHOT'I MAÇ NUMARASIYLA OKUR — üretim TETİKLEMEZ.
        ///
        /// Neden gerekli: anahtar <c>{surface}:{matchId}:{contextHash}</c> biçimindedir ve
        /// hash'i hesaplamak için tam MatchDetail bağlamı kurmak gerekir. Keşfet akışı bu
        /// bağlamı kurmaz (kurması 50 kart için ağır olurdu); yalnızca DAHA ÖNCE üretilmiş
        /// bir anlatı varsa onu göstermek ister. Bu yol o yüzden hash'siz arar.
        ///
        /// Bulunamazsa false döner ve çağıran taraf anlatı GÖSTERMEZ — asla üretim başlatmaz.
        /// </summary>
        bool TryGetLatestForMatch(RadarSurface surface, int matchId, out RadarNarrativeResult result);
    }

    /// <summary>Bellek içi, TTL'li, dış bağımlılıksız uygulama (singleton).</summary>
    public sealed class InMemoryRadarNarrativeStore : IRadarNarrativeStore
    {
        /// <summary>
        /// TTL 20 dk → 12 saat.
        ///
        /// NEDEN GÜVENLİ: anahtar <c>{surface}:{matchId}:{contextHash}</c>. Maçın verisi
        /// değişirse hash de değişir ve YENİ anahtar üretilir; uzun TTL bayat içerik
        /// SERVİS ETMEZ, yalnız aynı bağlamın tekrar üretilmesini engeller.
        ///
        /// NEDEN GEREKLİ: 20 dk'lık TTL yüzünden aynı maç 20 dakikada bir yeniden LLM'e
        /// gidiyordu ve o istek /detail'i ~9 sn bekletiyordu (ölçüldü: narrative=9110ms,
        /// toplamın %92'si). Uzun TTL soğuk isabeti maç başına bir kereye indirir.
        /// </summary>
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

        /// <summary>Süresi dolmuş kayıtların temizleneceği eşik (sınırsız büyüme koruması).</summary>
        private const int PruneThreshold = 500;

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
        {
            _cache[key] = new Entry(result, DateTime.UtcNow);

            // TTL uzadığı için süresi dolmuş kayıtlar kendiliğinden düşmez → eşik aşılınca
            // temizlenir. Sözlük büyüklüğü sınırsız artmaz.
            if (_cache.Count <= PruneThreshold) return;

            var now = DateTime.UtcNow;
            foreach (var kv in _cache)
                if (now - kv.Value.StoredAt >= Ttl)
                    _cache.TryRemove(kv.Key, out _);
        }

        /// <summary>
        /// Aynı maç için birden fazla snapshot bulunabilir (bağlam değiştikçe yeni hash).
        /// EN YENİSİ döner; TTL dolmuş kayıtlar yok sayılır. Üretim yapılmaz.
        /// </summary>
        public bool TryGetLatestForMatch(RadarSurface surface, int matchId, out RadarNarrativeResult result)
        {
            var prefix = $"{surface}:{matchId}:";
            var now = DateTime.UtcNow;

            RadarNarrativeResult? best = null;
            var bestAt = DateTime.MinValue;

            foreach (var kv in _cache)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (now - kv.Value.StoredAt >= Ttl) continue;
                if (kv.Value.StoredAt <= bestAt) continue;

                best = kv.Value.Result;
                bestAt = kv.Value.StoredAt;
            }

            result = best!;
            return best != null;
        }

        private readonly record struct Entry(RadarNarrativeResult Result, DateTime StoredAt);
    }
}
