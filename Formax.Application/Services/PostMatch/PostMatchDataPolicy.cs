using System;
using Formax.Domain.Constants;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// BİTMİŞ MAÇ VERİ TOPLAMA POLİTİKASI — aday seçiminin ve bütçenin TEK yeri.
    ///
    /// Saf karar sınıfıdır: ağ yok, veritabanı yok, saat okuma yok. Bu sayede
    /// "hangi maç aday olur?" sorusu testten doğrudan sorulabilir ve iki yerde iki
    /// farklı cevap oluşamaz.
    ///
    /// KESİN AKIŞ (ürün kararı 06.09.2026):
    ///   1. Maç DB'de Finished olur.
    ///   2. Arka plan işi bunu ADAY seçer.
    ///   3. Olay ve istatistik sağlayıcıdan KONTROLLÜ alınır.
    ///   4. Kanonik tablolara yazılır.
    ///   5. Kullanıcı maç detayını açtığında YALNIZ DB okunur.
    ///
    /// MAÇ SAYFASININ AÇILMASI ADAY ÜRETMEZ. Bu sınıfın hiçbir metodu okuma yolundan
    /// çağrılmaz; tetikleyici yalnız arka plan işinin turudur.
    /// </summary>
    public static class PostMatchDataPolicy
    {
        /// <summary>
        /// Maç bitişinden veri toplamaya kadar beklenen en az süre.
        ///
        /// NEDEN VAR: son düdükle birlikte sağlayıcının istatistik tablosu henüz
        /// kesinleşmemiş olabiliyor. Hemen sorulursa eksik tablo çekilir, "veri var"
        /// diye yazılır ve bir daha istenmez — eksik veri KALICI hâle gelirdi.
        /// </summary>
        public static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(15);

        /// <summary>Uzatma/VAR dahil bir maçın makul en geç bitişi (video hattıyla AYNI pay).</summary>
        public static readonly TimeSpan MatchDuration = MatchVideoIdentityValidator.MatchDuration;

        /// <summary>Olay çekiminin UTC gün başına KALICI tavanı.</summary>
        public const int DefaultEventsPerUtcDay = 5;

        /// <summary>İstatistik çekiminin UTC gün başına KALICI tavanı.</summary>
        public const int DefaultStatisticsPerUtcDay = 5;

        /// <summary>Aynı maç + aynı uç için en kısa yeniden deneme aralığı.</summary>
        public static readonly TimeSpan PerFixtureCooldown = TimeSpan.FromHours(24);

        /// <summary>Bu maçın son düdüğü.</summary>
        public static DateTime EndOf(DateTime kickoffUtc) => kickoffUtc + MatchDuration;

        /// <summary>
        /// ADAY MI? — beş koşulun HEPSİ sağlanmalı.
        ///
        /// <paramref name="alreadyHasData"/> true iken sağlayıcıya HİÇ gidilmez:
        /// başarılı veri bir daha istenmez. Bu, "cache varsa gerçek istek yapılmaz"
        /// kuralının kalıcı depo karşılığıdır.
        /// </summary>
        public static bool IsCandidate(
            string status,
            int leagueId,
            string? externalFixtureId,
            bool alreadyHasData,
            bool scoreIsDefinite,
            DateTime kickoffUtc,
            DateTime nowUtc)
        {
            // 1) YALNIZ BİTMİŞ MAÇ. Canlı yoklama bu hattın işi DEĞİLDİR.
            if (!string.Equals(status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase))
                return false;

            // 2) YALNIZ KİLİTLİ 11 ORGANİZASYON.
            if (!LockedCompetitions.All.Contains(leagueId)) return false;

            // 3) Sağlayıcı kimliği olmayan maç sorulamaz.
            if (string.IsNullOrWhiteSpace(externalFixtureId)) return false;

            // 4) Elimizde zaten veri varsa istek YOK.
            if (alreadyHasData) return false;

            // 5) Skoru kesinleşmemiş maçın olay/istatistiği de kesinleşmemiştir.
            if (!scoreIsDefinite) return false;

            // 6) Bitişin üzerinden en az 15 dakika geçmiş olmalı.
            return nowUtc >= EndOf(kickoffUtc) + SettleDelay;
        }
    }
}
