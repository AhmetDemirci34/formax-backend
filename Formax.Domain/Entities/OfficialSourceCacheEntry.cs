namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ KAYNAK KALICI ÖNBELLEĞİ — adres başına son başarılı cevap.
    ///
    /// Koşullu GET için <c>ETag</c>/<c>Last-Modified</c> burada saklanır; kaynak 304 dediğinde
    /// gövde buradan verilir. <see cref="ContentHash"/> değişmediyse ve bu özet daha önce
    /// işlendiyse (<see cref="ProcessedHash"/>) aynı içerik yeniden ayrıştırılmaz.
    /// Restart'ta kaybolmaz.
    /// </summary>
    public class OfficialSourceCacheEntry
    {
        /// <summary>Adresin SHA-256 özeti (birincil anahtar).</summary>
        public string UrlHash { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string SourceKey { get; set; } = string.Empty;

        public string? ETag { get; set; }

        public string? LastModified { get; set; }

        public string? ContentType { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Son başarılı cevabın gövdesi (boyut sınırı fetcher'dadır).</summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>Gövdenin ağdan alındığı son an.</summary>
        public DateTime FetchedAtUtc { get; set; }

        /// <summary>Kaynağın içeriği son kez doğruladığı an (200 ya da 304).</summary>
        public DateTime ValidatedAtUtc { get; set; }

        /// <summary>İşleyenin başarıyla tükettiği son içerik özeti; null = hiç işlenmedi.</summary>
        public string? ProcessedHash { get; set; }
    }
}
