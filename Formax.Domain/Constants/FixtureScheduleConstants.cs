namespace Formax.Domain.Constants
{
    /// <summary>Tekil fikstür isteği amaçları — ayrı bütçe havuzları.</summary>
    public static class FixtureRefreshPurposes
    {
        /// <summary>Geçmiş maçın eksik sonucunu kapatma.</summary>
        public const string Result = "Result";

        /// <summary>Gelecek maçın geçici tarih/saatini doğrulama.</summary>
        public const string FutureSchedule = "FutureSchedule";

        /// <summary>Bitmiş maçın maç sonrası içeriğini toplama (haber/olay/istatistik).</summary>
        public const string PostMatchContent = "PostMatchContent";

        /// <summary>
        /// Bitmis macin RESMI VIDEOSUNU arama. AYRI BUTCE: video aramasi api-football
        /// kotasina DOKUNMAZ (bambaska kaynaklara gider) ve haber butcesiyle karisirsa
        /// biri digerini ac birakir.
        /// </summary>
        public const string PostMatchVideo = "PostMatchVideo";

        /// <summary>
        /// MAÇ ÖNCESİ RESMÎ KADRO (<c>fixtures/lineups</c>).
        ///
        /// NEDEN KALICI DEFTERE TAŞINDI (06.09.2026): kadro denemeleri süreç belleğinde
        /// bir sözlükte (matchId → günlük sayaç) tutuluyordu. Backend her yeniden
        /// başladığında sayaç sıfırlanıyor ve aynı fikstür aynı gün yeniden yoklanıyordu.
        /// Kadro penceresi T−90'a genişletildiği için bu kaçak daha da pahalıya gelirdi.
        /// Restart, günlük hakkı SIFIRLAMAZ.
        /// </summary>
        public const string Lineup = "Lineup";

        /// <summary>
        /// BİTMİŞ MAÇ OLAYLARI (<c>fixtures/events</c>) — kendi günlük bütçesi.
        ///
        /// İstatistikten AYRI havuzdur: biri diğerinin hakkını yiyemez. Olay çekimi
        /// başarılı olup istatistik başarısız olduğunda kısmi başarı korunur.
        /// </summary>
        public const string PostMatchEvents = "PostMatchEvents";

        /// <summary>
        /// BİTMİŞ MAÇ İSTATİSTİKLERİ (<c>fixtures/statistics</c>) — kendi günlük bütçesi.
        /// </summary>
        public const string PostMatchStatistics = "PostMatchStatistics";
    }

    /// <summary>
    /// KICKOFF SAATİNİN GÜVENİLİRLİĞİ.
    ///
    /// Sağlayıcı, takvimi henüz kesinleşmemiş turlar için NOMİNAL bir tarih/saat döndürür
    /// (api-football bunu <c>status.short = "TBD"</c> ile işaretler; eşleyicimiz bu bilgiyi
    /// "NotStarted"a indirgediği için kayboluyordu). Ölçüldü 01.09.2026: depodaki 161
    /// Süper Lig gelecek maçının TAMAMI 12:00:00'da duruyordu ve bu ligin oynanmış
    /// maçlarında 12:00 slotu HİÇ yok — yani saat gerçek değil, yer tutucuydu.
    ///
    /// Kullanıcıya yer-tutucu bir saat KESİN saat gibi gösterilmez.
    /// </summary>
    public static class KickoffPrecisions
    {
        /// <summary>Sağlayıcı kesin tarih/saat verdi.</summary>
        public const string Confirmed = "Confirmed";

        /// <summary>Tarih/saat geçici (tur nominali) — doğrulanması gerekir.</summary>
        public const string Provisional = "Provisional";
    }
}
