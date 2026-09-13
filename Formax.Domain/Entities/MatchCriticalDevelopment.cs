namespace Formax.Domain.Entities
{
    /// <summary>
    /// DOĞRULANMIŞ KRİTİK GELİŞME — resmî kaynağın yapılandırılmış maç verisinden tespit edilen,
    /// maçı doğrudan etkileyen değişiklik (erteleme, iptal, başlama saati, stat).
    ///
    /// Söylenti/haber başlığı/yorum bu tabloya giremez: satır yalnız kayıt defterinde doğrulanmış
    /// bir resmî kaynağın alan değeri değiştiğinde yazılır. Aynı gelişme (<see cref="EvidenceHash"/>)
    /// ikinci kez yazılamaz (UNIQUE); kaynağın aynı değeri tekrar bildirmesi yeni gelişme değildir.
    /// </summary>
    public class MatchCriticalDevelopment
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string SourceKey { get; set; } = string.Empty;

        /// <summary>Gelişmenin okunduğu resmî adres (maç sayfası ya da maç merkezi ucu).</summary>
        public string? OfficialUrl { get; set; }

        /// <summary>Kaynağın yayın anı — kaynak vermiyorsa null (uydurulmaz).</summary>
        public DateTime? SourcePublishedAtUtc { get; set; }

        /// <summary>"Postponed" | "Cancelled" | "Suspended" | "KickoffChanged" | "VenueChanged".</summary>
        public string DevelopmentType { get; set; } = string.Empty;

        /// <summary>Etkilenen takım (FORMAX Team.Id) — maç geneli ise null.</summary>
        public int? AffectedTeamId { get; set; }

        /// <summary>Etkilenen oyuncunun resmî kimliği — yoksa null.</summary>
        public string? AffectedPlayerId { get; set; }

        /// <summary>"Critical" (erteleme/iptal — sınırsız) | "High" | "Medium".</summary>
        public string Severity { get; set; } = "High";

        public string VerificationStatus { get; set; } = "Verified";

        /// <summary>SHA-256(maç | tür | yeni değer) — gelişmenin kimliği.</summary>
        public string EvidenceHash { get; set; } = string.Empty;

        public string? PreviousValue { get; set; }
        public string? NewValue { get; set; }

        /// <summary>Kanıta bağlı kısa Türkçe özet (haber başlığı kopyalanmaz).</summary>
        public string SummaryTr { get; set; } = string.Empty;

        public DateTime DiscoveredAtUtc { get; set; }

        /// <summary>Takipçi dağıtımının tamamlandığı an; bildirim sınırına takıldıysa null kalır.</summary>
        public DateTime? NotifiedAtUtc { get; set; }

        /// <summary>Dağıtım yapılmadıysa gerekçe ("NotificationCapReached" vb.).</summary>
        public string? NotificationNote { get; set; }
    }
}
