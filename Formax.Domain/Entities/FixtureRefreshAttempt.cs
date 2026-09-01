using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// TEKİL FİKSTÜR İSTEĞİ DENEME DEFTERİ — KALICI (restart-safe).
    ///
    /// NEDEN VAR (ölçüldü 01.09.2026): tekil <c>fixtures?id=</c> denemelerinin tavanı
    /// yalnız TUR başınaydı ve süreç belleğindeydi. Backend her açıldığında 30 sn sonra
    /// yeni bir tur başlıyor ve aynı 10 isteklik patlama tekrarlanıyordu; o gün 5 ayrı
    /// açılış patlaması 44 tekil istek üretti (kotanın neredeyse yarısı). Restart, hız
    /// sınırını sıfırlamamalıdır.
    ///
    /// Bu defter üç şeyi birden çözer:
    ///  • FİKSTÜR BAZLI SOĞUMA — aynı fikstür cooldown dolmadan yeniden istenmez.
    ///  • GÜNLÜK TAVAN — UTC gün başına amaç bazında toplam deneme sınırı kalıcıdır.
    ///  • EŞZAMANLILIK — deneme HTTP'den ÖNCE atomik olarak rezerve edilir; iki süreç
    ///    aynı fikstürü aynı anda alamaz (SQL koşullu yazma kilidi).
    ///
    /// BAŞARISIZ DENEME DE SAYILIR: sağlayıcı hata gövdesi (200 + errors) döndürse bile
    /// gerçek bir HTTP isteği harcanmıştır. Sayılmazsa bozuk bir fikstür kotayı sonsuza
    /// kadar döver.
    ///
    /// Kimlik: ExternalMatchId + Purpose + DayUtc. Gün anahtarda olduğu için günlük tavan
    /// TAM sayılır (aynı fikstür bir günde iki kez denenirse ikisi de görünür); soğuma ise
    /// günlerden bağımsız olarak en son deneme anından hesaplanır.
    /// </summary>
    public sealed class FixtureRefreshAttempt
    {
        /// <summary>Sağlayıcı fikstür kimliği (Match.ExternalMatchId ile aynı uzay).</summary>
        public string ExternalMatchId { get; set; } = string.Empty;

        /// <summary>
        /// Denemenin amacı — <see cref="Formax.Domain.Constants.FixtureRefreshPurposes"/>.
        /// İki amaç AYRI bütçelerle yönetilir: geçmişin sonuç borcu ile geleceğin takvim
        /// düzeltmesi aynı havuzdan yenmemelidir.
        /// </summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>Denemenin ait olduğu UTC gün (gün başı).</summary>
        public DateTime DayUtc { get; set; }

        /// <summary>Bu gün içinde bu fikstür için yapılan deneme sayısı.</summary>
        public int AttemptCount { get; set; }

        /// <summary>En son deneme anı (UTC) — soğuma bundan hesaplanır.</summary>
        public DateTime LastAttemptUtc { get; set; }

        /// <summary>Son denemenin sonucu: "Applied" | "NoData" | "ProviderError" | "Reserved".</summary>
        public string LastOutcome { get; set; } = string.Empty;
    }
}
