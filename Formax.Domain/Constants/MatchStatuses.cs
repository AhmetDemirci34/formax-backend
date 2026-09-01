namespace Formax.Domain.Constants
{
    /// <summary>
    /// Maç durumu sabitleri — depoda YAZILI olan metinler.
    ///
    /// Sağlayıcı kodları (api-football "FT"/"PST"/"CANC"/"ABD"…) provider katmanında
    /// bu değerlere eşlenir; uygulama kodu yalnız buradaki metinleri bilir.
    /// </summary>
    public static class MatchStatuses
    {
        public const string PreMatch = "PreMatch";
        public const string NotStarted = "NotStarted";
        public const string Live = "Live";
        public const string Finished = "Finished";

        /// <summary>Ertelendi — ileri bir tarihte oynanacak.</summary>
        public const string Postponed = "Postponed";

        /// <summary>İptal edildi — oynanmayacak.</summary>
        public const string Cancelled = "Cancelled";

        /// <summary>
        /// Yarıda kaldı. api-football "ABD" kodunu şu an <see cref="Cancelled"/>'a eşliyor;
        /// bu sabit, ayrı bir durum yazılmaya başlandığında hesapların kendiliğinden
        /// doğru davranması için vardır (sayım tarafı ikisini de "oynanmadı" sayar).
        /// </summary>
        public const string Abandoned = "Abandoned";

        /// <summary>
        /// MAÇ OYNANMADI — ertelendi / iptal edildi / yarıda kaldı.
        ///
        /// Bu maçların sonucu YOKTUR ve BEKLENMEZ. Puan durumu tamlığı hesabında
        /// "eksik sonuç" sayılmazlar: ertelenmiş bir maç yüzünden tablo "tamamlanıyor"
        /// diye işaretlenirse, aslında güncel olan bir tablo yanlışlıkla şüpheli gösterilir.
        /// Takımların oynadığı maç sayısının farklı olması ligde NORMALDİR.
        /// </summary>
        public static bool IsNotPlayed(string? status)
            => string.Equals(status, Postponed,  System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Cancelled,  System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Abandoned,  System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// SONUCU HENÜZ GELMEMİŞ oynanmış/oynanacak maç durumu — sonuç alım hattının borcu.
        /// </summary>
        public static bool IsAwaitingResult(string? status)
            => string.Equals(status, NotStarted, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, PreMatch,   System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Live,       System.StringComparison.OrdinalIgnoreCase);
    }
}
