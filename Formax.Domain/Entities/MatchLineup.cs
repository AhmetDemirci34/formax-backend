namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tracks whether official lineups have been released for a match.
    /// One row per match — upserted when the ingestion job fetches lineup data.
    /// </summary>
    public class MatchLineup
    {
        /// <summary>Primary key — same as Match.Id.</summary>
        public int MatchId { get; set; }

        public bool HomeLineupsReleased { get; set; }
        public bool AwayLineupsReleased { get; set; }

        /// <summary>
        /// Sağlayıcının açıkladığı diziliş ("4-4-2", "4-2-3-1"). Takım başına AYRI tutulur —
        /// biri diğerine kopyalanmaz. Sağlayıcı vermezse null kalır; tahmin edilmez.
        /// Takım seviyesindeki veri olduğu için oyuncu satırında değil, bu başlıkta durur.
        /// </summary>
        public string? HomeFormation { get; set; }
        public string? AwayFormation { get; set; }

        /// <summary>UTC time when the lineup was first detected as released.</summary>
        public DateTime? ReleasedAt { get; set; }

        /// <summary>UTC time of the most recent fetch from the provider.</summary>
        public DateTime FetchedAt { get; set; }

        // ── KAYNAK KİMLİĞİ (11.09.2026 · additive) ─────────────────────────────────

        /// <summary>Sağlayıcı fikstür kimliği (api-football fixture id).</summary>
        public string? ExternalFixtureId { get; set; }

        /// <summary>Sağlayıcının kadro satırındaki takım kimlikleri — taraf eşlemesinin kanıtı.</summary>
        public int? HomeTeamExternalId { get; set; }
        public int? AwayTeamExternalId { get; set; }

        /// <summary>Sağlayıcı verdiyse teknik direktör adı; vermediyse null (uydurulmaz).</summary>
        public string? HomeCoach { get; set; }
        public string? AwayCoach { get; set; }

        /// <summary>Verinin geldiği lisanslı sağlayıcı ("api-football").</summary>
        public string? Provider { get; set; }

        /// <summary>
        /// SON GERÇEK KONTROL (UTC) — sağlayıcı GEÇERLİ bir cevap verdiğinde (kadro ya da boş)
        /// yazılır. Bütçe/plan engeli burada iz bırakmaz: slot takvimi bu alana bakar ve
        /// engellenen tur, sıradaki slotu harcamış sayılmaz.
        /// </summary>
        public DateTime? LastCheckedAtUtc { get; set; }

        // ── RESMÎ KAYNAK KANITI (additive) ──────────────────────────────────────────
        // Kadro artık lisanslı veri sağlayıcısından değil lig/federasyon/kulübün resmî
        // yayınından gelir. Hangi kaynaktan, hangi içerikten ve ne zaman doğrulandığı
        // taraf bazında değil başlıkta tutulur; iki kulübün ayrı açıklaması birleştiğinde
        // kaynaklar virgülle yan yana yazılır.

        /// <summary>Kayıt defterindeki resmî kaynak anahtarı (ör. "seriea-sdp").</summary>
        public string? SourceKey { get; set; }

        /// <summary>Kadronun okunduğu resmî adres.</summary>
        public string? SourceUrl { get; set; }

        /// <summary>Kadronun ayrıştırıldığı ham içeriğin SHA-256 özeti.</summary>
        public string? RawContentHash { get; set; }

        /// <summary>Kaynağın yayın anı (kaynak veriyorsa); vermiyorsa null — uydurulmaz.</summary>
        public DateTime? SourcePublishedAtUtc { get; set; }

        /// <summary>Kadronun kaynakta ilk kez görüldüğü an.</summary>
        public DateTime? DiscoveredAtUtc { get; set; }

        /// <summary>Doğrulamanın geçtiği an.</summary>
        public DateTime? VerifiedAtUtc { get; set; }

        /// <summary>"Verified" | "PartiallyVerified" (yalnız bir taraf) | "Pending".</summary>
        public string? VerificationStatus { get; set; }

        // ── GEÇMİŞ DOLDURMA (19.09.2026 · additive) ────────────────────────────────

        /// <summary>
        /// VERİ KALİTESİ SEVİYESİ — kadronun hangi alanları GERÇEKTEN taşıdığı:
        /// "StartersOnly" (yalnız ilk 11) | "WithBench" (+ yedekler) | "WithMinutes"
        /// (+ gerçek değişiklik dakikaları). Eksik alan doldurulmuş gibi gösterilmez.
        /// </summary>
        public string? DataQuality { get; set; }

        /// <summary>Kayıt canlı turda değil geçmiş doldurma turunda yazıldıysa o anın damgası.</summary>
        public DateTime? BackfilledAtUtc { get; set; }

        /// <summary>
        /// Takipçilere "Kadrolar açıklandı" dağıtımının tamamlandığı an. Kadro yazıldıktan sonra
        /// süreç çökerse bir sonraki tur dağıtımı bu alan boş olduğu için tamamlar; kullanıcı
        /// başına tekillik yine DB anahtarındadır.
        /// </summary>
        public DateTime? FollowersNotifiedAtUtc { get; set; }

        // Navigation
        public Match? Match { get; set; }
    }
}
