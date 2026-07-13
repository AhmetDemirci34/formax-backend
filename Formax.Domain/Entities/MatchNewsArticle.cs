using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX Data Engine v2 — bir maça (FORMAX_MATCH_ID) bağlı, tekilleştirilmiş haber.
    /// Bir maçın 50–200 haberi olabilir. ContentHash benzersizdir (aynı hikâye iki kez
    /// yazılmaz). Reasoning Engine bu kayıtlardan MatchNewsContext üretir.
    /// </summary>
    public class MatchNewsArticle
    {
        public int Id { get; set; }

        /// <summary>Bağlı olduğu maçın FORMAX kimliği (v1 Fixture Engine).</summary>
        public string FormaxMatchId { get; set; } = "";

        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishedUtc { get; set; }

        /// <summary>Bu hikâyeyi yayınlayan kaynaklar (virgülle ayrılmış).</summary>
        public string Sources { get; set; } = "";
        public int SourceCount { get; set; }

        public string Language { get; set; } = "en";

        /// <summary>Çok-etiketli kümeler (virgülle ayrılmış): Injury,Transfer …</summary>
        public string Clusters { get; set; } = "";

        public int Confidence { get; set; }

        /// <summary>Tekilleştirme anahtarı — benzersiz indeks.</summary>
        public string ContentHash { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
