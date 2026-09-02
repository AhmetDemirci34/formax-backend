namespace Formax.Domain.Constants
{
    /// <summary>
    /// MAÇ VİDEOSU TÜRLERİ — ürün kararı (02.09.2026) ile kapalı liste.
    ///
    /// Bitmiş maç ekranı YALNIZ maç görüntüsü gösterir. Basın toplantısı, teknik
    /// direktör/oyuncu açıklaması, kamera arkası ve stüdyo programı BU LİSTEDE YOKTUR;
    /// kaynak resmî olsa bile bu ekrana girmez.
    /// </summary>
    public static class MatchVideoTypes
    {
        /// <summary>Maç özeti — ekranın ANA video kartı.</summary>
        public const string MatchHighlights = "MatchHighlights";

        /// <summary>Uzun özet (extended highlights).</summary>
        public const string ExtendedHighlights = "ExtendedHighlights";

        /// <summary>Tek gol klibi.</summary>
        public const string Goal = "Goal";

        /// <summary>Penaltı klibi (atılan ya da kaçan).</summary>
        public const string Penalty = "Penalty";

        /// <summary>Kırmızı kart klibi.</summary>
        public const string RedCard = "RedCard";

        /// <summary>VAR kararı klibi.</summary>
        public const string Var = "VAR";

        /// <summary>Diğer önemli an klibi (kurtarış, direk, tartışmalı pozisyon…).</summary>
        public const string ImportantMoment = "ImportantMoment";

        public static bool IsKnown(string? value)
            => IsMainHighlight(value) || IsMoment(value);

        /// <summary>
        /// Ekranda ANA player'a çıkacak tür mü?
        ///
        /// Uzun özet de bir MAÇ ÖZETİDİR: ayrı bir "önemli an klibi" değildir ve
        /// GOLLER VE ÖNEMLİ ANLAR listesine düşmez.
        /// </summary>
        public static bool IsMainHighlight(string? value)
            => value == MatchHighlights || value == ExtendedHighlights;

        /// <summary>"Goller ve önemli anlar" listesine giren AYRI klip türleri.</summary>
        public static bool IsMoment(string? value)
            => value == Goal || value == Penalty || value == RedCard
            || value == Var || value == ImportantMoment;
    }

    /// <summary>
    /// VİDEO DOĞRULAMA SONUCU — kaydın NEDEN oynatıldığını/oynatılmadığını taşır.
    ///
    /// "Bulamadık" ile "bulduk ama oynatamıyoruz" AYNI ŞEY DEĞİLDİR ve kullanıcıya da
    /// aynı gösterilmez. Bu alan olmadan ekran ikisini de sessiz boşluğa çevirirdi.
    /// </summary>
    public static class MatchVideoVerificationStatuses
    {
        /// <summary>Resmî kaynak + embed izni doğrulandı → uygulama içinde oynar.</summary>
        public const string Verified = "Verified";

        /// <summary>
        /// Kaynak resmî ve doğru maça ait, ancak yayıncı gömmeye izin vermiyor
        /// (ör. UEFA.com <c>frame-ancestors 'self'</c>). Oynatılmaz; engel AŞILMAZ.
        /// </summary>
        public const string EmbedBlocked = "EmbedBlocked";

        /// <summary>Arama yapıldı, resmî ve doğrulanabilir video bulunamadı.</summary>
        public const string Unavailable = "Unavailable";

        /// <summary>
        /// Kayıt YANLIŞ olduğu KANITLANDI (ör. stüdyo programı maç özeti diye bağlanmış).
        /// Oynatılmaz, kartta işaret üretmez, ekranda görünmez.
        /// </summary>
        public const string Rejected = "Rejected";

        /// <summary>
        /// Kanıt YETERSİZ — ne kesin doğru ne kesin yanlış. Kayıt SİLİNMEZ (insan bakabilsin)
        /// ama kullanıcıya asla gösterilmez: şüpheli içeriği "çalışan video" gibi sunmak,
        /// hiç göstermemekten daha kötüdür.
        /// </summary>
        public const string NeedsManualReview = "NeedsManualReview";

        /// <summary>Kullanıcıya gösterilmeye UYGUN tek durum.</summary>
        public static bool IsShowable(string? value) => value == Verified;
    }

    /// <summary>
    /// RET GEREKÇESİ — bir kaydın NEDEN gösterilmediğinin makine okunur nedeni.
    ///
    /// Serbest metin değil kapalı liste: geriye dönük tarama, "bu kayıt neden kapandı?"
    /// sorusunu gruplayarak yanıtlayabilsin ve aynı hata sınıfı bir daha açılmasın.
    /// </summary>
    public static class MatchVideoRejectionReasons
    {
        /// <summary>Maç görüntüsü değil: stüdyo programı, yorum, röportaj, tahmin yayını.</summary>
        public const string NotMatchHighlights = "NotMatchHighlights";

        /// <summary>Başlık birden çok farklı karşılaşmayı listeliyor; tek maça bağlanamaz.</summary>
        public const string MultipleMatchesInTitle = "MultipleMatchesInTitle";

        /// <summary>Başlıkta gerçek bir özet işareti (Özet/Highlights) yok.</summary>
        public const string NoHighlightMarker = "NoHighlightMarker";

        /// <summary>Karşılaşmanın yalnız bir tarafı doğrulanabiliyor.</summary>
        public const string TeamsNotConfirmed = "TeamsNotConfirmed";

        /// <summary>Aynı video birden çok maça bağlanmış; hangisine ait olduğu belirsiz.</summary>
        public const string SharedAcrossMatches = "SharedAcrossMatches";
    }
}
