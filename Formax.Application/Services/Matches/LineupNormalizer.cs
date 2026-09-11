using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Lineup;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// SAĞLAYICI KADROSUNU MAÇA OTURTMA — saf fonksiyonlar (ağ/DB yok).
    ///
    /// TARAF: api-football pratikte ilk satırı ev sahibi verir ama bu bir sözleşme
    /// değildir. Taraf, sağlayıcının her satırda verdiği TAKIM KİMLİĞİ ile maçın ev
    /// sahibi/deplasman kimliği karşılaştırılarak belirlenir; ters geldiyse bütün alanlar
    /// (ilk 11, yedek, diziliş, teknik direktör, kimlik) birlikte yer değiştirir.
    ///
    /// TEKİLLİK: aynı oyuncu bir tarafta iki kez yazılmaz. İlk 11'de olan oyuncu yedekte
    /// tekrar görünürse ilk 11 kaydı kalır.
    /// </summary>
    public static class LineupNormalizer
    {
        /// <summary>Tarafın nasıl belirlendiği — teşhis için.</summary>
        public enum Orientation
        {
            /// <summary>Sağlayıcı sırası takım kimliğiyle doğrulandı.</summary>
            ConfirmedByTeamId,
            /// <summary>Sağlayıcı ters sırada verdi; takım kimliğiyle düzeltildi.</summary>
            SwappedByTeamId,
            /// <summary>Kimlik yok/eşleşmedi; sağlayıcı sırası kullanıldı.</summary>
            ProviderOrder
        }

        public static (SportsLineupResult Result, Orientation How) Orient(
            SportsLineupResult source, int? matchHomeExternalId, int? matchAwayExternalId)
        {
            var first = source.HomeTeamExternalId;
            var second = source.AwayTeamExternalId;

            if (matchHomeExternalId is int home && matchAwayExternalId is int away)
            {
                if ((first == home || second == away) && first != away && second != home)
                    return (source, Orientation.ConfirmedByTeamId);

                if ((first == away || second == home) && first != home && second != away)
                    return (Swap(source), Orientation.SwappedByTeamId);
            }

            return (source, Orientation.ProviderOrder);
        }

        private static SportsLineupResult Swap(SportsLineupResult s) => new()
        {
            LineupsAnnounced = s.LineupsAnnounced,
            HomeFormation = s.AwayFormation,
            AwayFormation = s.HomeFormation,
            HomeStarters = s.AwayStarters,
            HomeBench = s.AwayBench,
            AwayStarters = s.HomeStarters,
            AwayBench = s.HomeBench,
            HomeTeamExternalId = s.AwayTeamExternalId,
            AwayTeamExternalId = s.HomeTeamExternalId,
            HomeCoach = s.AwayCoach,
            AwayCoach = s.HomeCoach
        };

        /// <summary>
        /// Bir tarafın ilk 11 ve yedeklerini tekilleştirir. Anahtar: forma numarası +
        /// normalize ad. İlk 11 önceliklidir.
        /// </summary>
        public static (List<SportsLineupPlayer> Starters, List<SportsLineupPlayer> Bench) DistinctSide(
            IEnumerable<SportsLineupPlayer> starters, IEnumerable<SportsLineupPlayer> bench)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var s = new List<SportsLineupPlayer>();
            var b = new List<SportsLineupPlayer>();

            foreach (var p in starters ?? Enumerable.Empty<SportsLineupPlayer>())
                if (seen.Add(Key(p))) s.Add(p);
            foreach (var p in bench ?? Enumerable.Empty<SportsLineupPlayer>())
                if (seen.Add(Key(p))) b.Add(p);

            return (s, b);
        }

        private static string Key(SportsLineupPlayer p)
            => p.ShirtNumber + "|" + (p.Name ?? string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>
    /// KADRO DURUMU — ekranın tek cümlesinin kaynağı (saf).
    ///
    /// SourceDelayed: yoklama penceresi açık ya da kickoff sonrası yakalama payı sürüyor,
    /// kadro yok. "Kadro yayımlanmadı" DENMEZ — dünyada yayımlanmış olabilir; FORMAX'ın
    /// lisanslı veri kaynağı henüz iletmemiştir.
    /// </summary>
    public static class LineupAvailability
    {
        public const string Released = "Released";
        public const string SourceDelayed = "SourceDelayed";
        public const string Waiting = "Waiting";
        public const string NotFound = "NotFound";

        public static string Resolve(bool hasLineup, DateTime kickoffUtc, DateTime nowUtc)
        {
            if (hasLineup) return Released;
            var remaining = kickoffUtc - nowUtc;
            if (remaining > LineupPollSchedule.WindowOpen) return Waiting;
            if (remaining >= -LineupPollSchedule.CatchUpGrace) return SourceDelayed;
            return NotFound;
        }

        /// <summary>
        /// "SON KONTROL" — sağlayıcının GERÇEKTEN sorulup geçerli cevap verdiği son an.
        ///
        /// Kaynak sırası: kadro başlığındaki <c>LastCheckedAtUtc</c> (boş cevapta da yazılır),
        /// başlığın alınma anı, en son eski kayıtlar için defter. Defterin son satırı yalnız
        /// gerçek bir cevapla kapandıysa (NoData/Applied) kullanılır: bütçe kapısında duran
        /// ya da hata veren rezervasyonun anı "kontrol edildi" gibi GÖSTERİLMEZ (11.09.2026,
        /// fikstür 1570381: bütçe engeli 22:07'yi "Son kontrol" diye gösteriyordu).
        /// </summary>
        public static DateTime? LastRealCheck(
            DateTime? headerLastCheckedUtc, DateTime? headerFetchedUtc,
            DateTime? ledgerLastAttemptUtc, string? ledgerLastOutcome)
            => headerLastCheckedUtc
               ?? headerFetchedUtc
               ?? (ledgerLastOutcome is "NoData" or "Applied" ? ledgerLastAttemptUtc : null);
    }
}
