using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Evidence Store kaydı. Bir maça (FORMAX_MATCH_ID) ait
    /// tek dijital sinyal: ham haber değil, signal-typed + kaynak-kalitesi ağırlıklı kanıt.
    /// ContentHash benzersiz (aynı kanıt iki kez yazılmaz). Reasoning bunlardan beslenir.
    /// </summary>
    public class MatchEvidenceRecord
    {
        public int Id { get; set; }

        public string FormaxMatchId { get; set; } = "";

        /// <summary>Birincil sinyal: Injury, Transfer, Lineup …</summary>
        public string Type { get; set; } = "";

        /// <summary>Tüm sinyaller (virgüllü).</summary>
        public string Cluster { get; set; } = "";

        public string Source { get; set; } = "";
        public int SourceQuality { get; set; }
        public int Confidence { get; set; }
        public DateTime PublishedUtc { get; set; }
        public string Headline { get; set; } = "";

        /// <summary>Tekilleştirme anahtarı — benzersiz indeks.</summary>
        public string ContentHash { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
