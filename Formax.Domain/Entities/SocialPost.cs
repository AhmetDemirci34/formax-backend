namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — Canonical Social Post. Resmi hesaplardan toplanan, bir maça (FORMAX_MATCH_ID)
    /// bağlı TEK sosyal paylaşım kaydı. Canonical News ile birlikte çalışır; ikisi de tek doğruluk
    /// kaynağı GDP'dir. Hem AI Context'i (SocialAiSignals) hem Match Detail (Flash Gelişmeler /
    /// Resmi Paylaşımlar) ekranını AYNI kayıttan besler (AI için ayrı, UI için ayrı sistem YOK).
    ///
    /// ContentHash benzersizdir (aynı paylaşım aynı maça iki kez yazılmaz). Yalnız resmi kaynak →
    /// IsOfficial=true, SourceTrust yüksek. Coverage yoksa satır oluşmaz (fake YOK).
    /// </summary>
    public class SocialPost
    {
        public int Id { get; set; }

        /// <summary>Bağlı maçın FORMAX kimliği (Canonical News ile aynı kimlik sistemi).</summary>
        public string FormaxMatchId { get; set; } = "";

        /// <summary>"YouTube" | "X" | "Instagram" | "Facebook" | "RSS".</summary>
        public string Platform { get; set; } = "";

        public string AccountHandle { get; set; } = "";
        public string AccountName { get; set; } = "";

        /// <summary>İlgili canonical Team.Id (takım-kapsamlı hesap). 0 = takım-dışı (lig/federasyon).</summary>
        public int RelatedTeamId { get; set; }

        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishedUtc { get; set; }

        /// <summary>Sinyal türü (SignalExtractor): Lineup/Injury/Transfer/Coach/Club Statement…</summary>
        public string SignalType { get; set; } = "";

        /// <summary>Kaynak güveni (resmi hesap = yüksek).</summary>
        public int SourceTrust { get; set; }

        /// <summary>Doğrulanmış resmi hesaptan mı geldi.</summary>
        public bool IsOfficial { get; set; }

        /// <summary>Tekilleştirme anahtarı — benzersiz indeks.</summary>
        public string ContentHash { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
