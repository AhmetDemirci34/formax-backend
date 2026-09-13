namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ KAYNAK İSTEK DEFTERİ — dış kaynağa çıkan (ya da önbellekten karşılanan) her
    /// resmî web isteğinin KALICI izi. Süreç yeniden başlasa da defter kaybolmaz: "bu turda
    /// bu adres indirildi mi", "host'a en son ne zaman gidildi", "hangi aday neden
    /// reddedildi" sorularının tek cevabı buradadır.
    ///
    /// Adres kayda sorgu dizesiyle birlikte girer; resmî kaynak uçlarında gizli anahtar
    /// TAŞINMAZ (anahtar isteyen uç zaten kullanılmaz). Gövde bu tabloda tutulmaz.
    /// </summary>
    public class OfficialSourceFetch
    {
        public long Id { get; set; }

        /// <summary>Kayıt defterindeki kaynak anahtarı (ör. "seriea-sdp").</summary>
        public string SourceKey { get; set; } = string.Empty;

        /// <summary>Kaynağı işleyen sağlayıcı sınıfı (ör. "SerieASdp").</summary>
        public string Provider { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;

        /// <summary>Adresin SHA-256 özeti — önbellek ve tur tekilliği anahtarı.</summary>
        public string UrlHash { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        /// <summary>"Lineup" | "Result" | "Events" | "Statistics" | "Schedule" | "Critical" | "Video".</summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>Taramanın tur kimliği; aynı tur aynı adresi bir kez indirir.</summary>
        public string? RoundKey { get; set; }

        /// <summary>İstek tek bir FORMAX maçı için yapıldıysa o maç; ortak akışta null.</summary>
        public int? MatchId { get; set; }

        public DateTime RequestedAtUtc { get; set; }

        public int? HttpStatus { get; set; }

        /// <summary>
        /// "Fetched" | "NotModified" | "RoundMemo" | "HostNotAllowed" | "NotHttps" |
        /// "PrivateAddress" | "RedirectRejected" | "TooLarge" | "Timeout" | "HttpError" |
        /// "NetworkError" | "RateLimited".
        /// </summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Gövde ağdan değil önbellekten geldiyse (304 ya da tur hafızası).</summary>
        public bool CacheHit { get; set; }

        /// <summary>Gövdenin SHA-256 özeti; gövde yoksa null.</summary>
        public string? ContentHash { get; set; }

        /// <summary>Özet bir önceki başarılı okumadan farklı mı?</summary>
        public bool ContentChanged { get; set; }

        public int Bytes { get; set; }

        public int DurationMs { get; set; }

        /// <summary>Bu cevaptan çıkarılan aday sayısı (işleyen tarafından yazılır).</summary>
        public int? CandidateCount { get; set; }

        /// <summary>Kabul edilen aday sayısı.</summary>
        public int? AcceptedCount { get; set; }

        /// <summary>Kabul/ret gerekçesi (makine okunur kısa metin).</summary>
        public string? Decision { get; set; }
    }
}
