using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Bir haberin (ContentHash) belirli bir dildeki çevirisi.
    ///
    /// NEDEN KALICI: çeviri LLM çağrısıdır (bulut, ücretli, saniyeler süren). Kullanıcı aynı
    /// haberi her açtığında yeniden çevirmek kabul edilemez; süreç-içi cache ise yeniden
    /// başlatmada kaybolur. Anahtar (ContentHash, Language) benzersizdir ve haber deposunun
    /// ZATEN kullandığı tekilleştirme anahtarıdır — yeni kimlik uydurulmaz.
    ///
    /// Aynı hikâye birden çok maça bağlı olabilir; çeviri ContentHash'e bağlı olduğu için
    /// maçlar arasında paylaşılır ve tekrar çevrilmez.
    /// </summary>
    public class MatchNewsTranslation
    {
        public int Id { get; set; }

        /// <summary>MatchNewsArticles.ContentHash — haberin kimliği.</summary>
        public string ContentHash { get; set; } = "";

        /// <summary>Hedef dil kodu (ISO-639-1, küçük harf): "tr" | "en" | "de" | …</summary>
        public string Language { get; set; } = "";

        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
