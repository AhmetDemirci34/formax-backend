using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// MAÇ VİDEOSU — YALNIZ DOĞRULANMIŞ RESMÎ KAYNAK.
    ///
    /// FORMAX içinde bir video ancak <see cref="IsOfficial"/> VE <see cref="IsEmbeddable"/>
    /// ikisi birden true iken oynatılır. İkisi de varsayılan olarak FALSE'tur: bir kayıt
    /// "oynatılabilir" hâle ancak kaynağı resmî listede doğrulanıp embed izni TEYİT
    /// edildiğinde gelir. <see cref="CanPlayInApp"/> bu kararın DONMUŞ hâlidir; okuma
    /// yolunda yeniden hesaplanmaz ki iki yüzey iki farklı cevap veremesin.
    ///
    /// YASAK (kod düzeyinde de karşılığı yok): video indirme, yeniden barındırma,
    /// DRM/CSP/X-Frame-Options aşma, proxy ile embed engelini dolanma, korsan kaynak,
    /// rastgele kullanıcı kanalını resmî sayma.
    ///
    /// EŞLEŞTİRME KİMLİĞİ (neden bu kadar çok alan var): çift maçlı turlarda iki ayak da
    /// AYNI iki takımı içerir — Fenerbahçe–Lyon 18.08.2026 ve Lyon–Fenerbahçe 26.08.2026.
    /// Başlıkta takım adının geçmesi kanıt DEĞİLDİR. Bir video ancak
    /// <see cref="ExternalFixtureId"/>, <see cref="MatchDateUtc"/> ve ev/deplasman yönü
    /// (<see cref="HomeTeamId"/>/<see cref="AwayTeamId"/>) birlikte doğrulandığında bağlanır.
    /// </summary>
    public sealed class MatchVideo
    {
        public long Id { get; set; }

        /// <summary>Kanonik Match.Id — ayak ayrımı burada kesinleşir.</summary>
        public int MatchId { get; set; }

        /// <summary>
        /// Sağlayıcı fikstür kimliği (Match.ExternalMatchId ile aynı uzay). Kaydın hangi
        /// FİKSTÜRE ait olduğunun takım adından bağımsız kanıtı.
        /// </summary>
        public string ExternalFixtureId { get; set; } = string.Empty;

        /// <summary>Maçın kickoff anı (UTC) — yayın tarihi bununla karşılaştırılır.</summary>
        public DateTime MatchDateUtc { get; set; }

        /// <summary>Ev sahibi takım (yön doğrulaması).</summary>
        public int HomeTeamId { get; set; }

        /// <summary>Deplasman takımı (yön doğrulaması).</summary>
        public int AwayTeamId { get; set; }

        /// <summary>Kaynaktaki video kimliği (ör. YouTube videoId) — tekilleştirme anahtarı.</summary>
        public string ExternalVideoId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Resmî yayıncı/kanal adı ("UEFA", "TRT SPOR", "Fenerbahçe SK"). Serbest metin
        /// DEĞİLDİR: yalnız izin listesindeki kaynakların adı yazılır.
        /// </summary>
        public string OfficialPublisher { get; set; } = string.Empty;

        /// <summary>Kaynağın kendi sayfası (kullanıcı dışarıda açmak isterse).</summary>
        public string SourcePageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Uygulama içi oynatma adresi. YouTube için YALNIZ youtube-nocookie.com gömme
        /// adresi kabul edilir. Embed izni yoksa null.
        /// </summary>
        public string? EmbedUrl { get; set; }

        public string? ThumbnailUrl { get; set; }

        /// <summary>Saniye cinsinden süre — kaynak vermediyse null (uydurulmaz).</summary>
        public int? DurationSeconds { get; set; }

        /// <summary><see cref="Formax.Domain.Constants.MatchVideoTypes"/>.</summary>
        public string VideoType { get; set; } = string.Empty;

        /// <summary>Yayın anı (UTC). Maç bitişinden ÖNCEYSE kayıt kabul edilmez.</summary>
        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>Kaynak, doğrulanmış resmî kanallar listesinde mi?</summary>
        public bool IsOfficial { get; set; }

        /// <summary>Kaynak uygulama içi gömmeye AÇIKÇA izin veriyor mu?</summary>
        public bool IsEmbeddable { get; set; }

        /// <summary>
        /// Kullanıcıya oynatılabilir mi? Yazma anında bir kez karar verilir
        /// (resmî + embed izinli + gömme adresi dolu). Okuma yolu bunu YENİDEN HESAPLAMAZ.
        /// </summary>
        public bool CanPlayInApp { get; set; }

        /// <summary>
        /// Videonun açık olduğu ülkeler (ISO 3166-1 alpha-2, virgülle ayrık). Kaynak bu
        /// bilgiyi vermiyorsa null — "her yerde açık" VARSAYILMAZ.
        ///
        /// ÖLÇÜLDÜ (02.09.2026): TRT SPOR'un resmî play-off özetleri yalnız "TR" için
        /// açıktır. Bu bilgi taşınmazsa yurt dışındaki kullanıcı, hata vermeyen ama
        /// hiç oynamayan bir player'a bakar ve suçu FORMAX'ta arar.
        /// </summary>
        public string? AvailableCountries { get; set; }

        /// <summary>Erişim belirli ülkelerle sınırlı mı? true ise ekran açıkça uyarır.</summary>
        public bool IsRegionRestricted { get; set; }

        // ── OLAY KLİBİ META VERİSİ ──────────────────────────────────────────────
        // YALNIZ ayrı bir gol/önemli an klibinde dolar ve YALNIZ kaynakta yazıyorsa.
        // Maç özeti videosundan dakika/oyuncu ÇIKARILMAZ: tam özeti sahte gol
        // kliplerine bölmek, olmayan içeriği varmış gibi göstermektir.

        /// <summary>Olayın dakikası — kaynakta yoksa null.</summary>
        public int? EventMinute { get; set; }

        /// <summary>Uzatma dakikası (90+3 → 3) — kaynakta yoksa null.</summary>
        public int? EventExtraMinute { get; set; }

        /// <summary>Olayın oyuncusu — kaynakta yoksa null.</summary>
        public string? EventPlayer { get; set; }

        /// <summary>Olayın takımı — kaynakta yoksa null.</summary>
        public string? EventTeam { get; set; }

        /// <summary><see cref="Formax.Domain.Constants.MatchVideoVerificationStatuses"/>.</summary>
        public string VerificationStatus { get; set; } = string.Empty;

        /// <summary>
        /// Kayıt neden gösterilmiyor? <see cref="Formax.Domain.Constants.MatchVideoRejectionReasons"/>.
        /// Gösterilebilir kayıtta BOŞTUR — dolu bir gerekçe her zaman "gösterme" demektir.
        /// </summary>
        public string? RejectionReason { get; set; }

        /// <summary>Doğrulamanın İNSAN OKUYABİLİR gerekçesi (teşhis; UI göstermez).</summary>
        public string VerificationNote { get; set; } = string.Empty;

        public DateTime VerifiedAtUtc { get; set; }

        // ── KANIT ZİNCİRİ (15.09.2026) ──────────────────────────────────────────
        /// <summary>
        /// Videonun hangi yolla bulunduğu: "OfficialWeb" (resmî site/sitemap/JSON-LD bağlantısı) | null = eski
        /// YouTube RSS kaydı. RSS robots.txt ile yasak olduğundan null kayıt resmî web kanıtı bulunana dek gösterilmez.
        /// </summary>
        public string? DiscoveryProvenance { get; set; }
        /// <summary>Videonun yayımlandığı resmî sayfa (kanıt).</summary>
        public string? EvidencePageUrl { get; set; }
        public string? EvidenceSourceKey { get; set; }
        /// <summary>Son yeniden doğrulama anı (kalıcı revalidation işi).</summary>
        public DateTime? RevalidatedAtUtc { get; set; }
        /// <summary>Oynatıcının bildirdiği hata kodu (101/150/152/100…) — SourceBlocked gerekçesi.</summary>
        public int? PlayerErrorCode { get; set; }
        public DateTime? BlockedAtUtc { get; set; }
    }

    /// <summary>
    /// BİR VİDEONUN GÖSTERİLEBİLİRLİĞİNİN TEK KURALI.
    ///
    /// NEDEN TEK YERDE: bu soruyu iki yüzey soruyor — maç özeti ekranı ("player açayım
    /// mı?") ve sonuç kartı ("'Video var' yazayım mı?"). İki yerde iki ayrı koşul
    /// listesi yazılırsa er geç ayrışırlar ve kart "Video var" derken ekran boş kalır.
    ///
    /// Kural KATIDIR: yalnız Verified. Rejected, NeedsManualReview, EmbedBlocked,
    /// Unavailable ve gerekçesi dolu her kayıt kullanıcıya GÖSTERİLMEZ.
    /// </summary>
    public static class MatchVideoRules
    {
        /// <summary>Resmî web kanıtlı keşif — YouTube RSS robots.txt ile yasak (15.09.2026 kullanıcı kararı).</summary>
        public const string OfficialWebProvenance = "OfficialWeb";

        /// <summary>
        /// Video resmî bir sitede yayımlanmış bağlantıdan mı bulundu? Eski RSS kaydı (provenance null) bu kanıt
        /// bulunana dek gösterilmez; kanıt bulunursa kayıt aynı satırda yeniden etkinleşir.
        /// </summary>
        public static bool HasOfficialWebEvidence(MatchVideo v)
            => v.DiscoveryProvenance == OfficialWebProvenance && !string.IsNullOrWhiteSpace(v.EvidencePageUrl);

        /// <summary>Uygulama içinde oynatılabilir ve kullanıcıya gösterilebilir mi?</summary>
        public static bool IsPlayable(MatchVideo v)
            => v is not null
            && HasOfficialWebEvidence(v)
            && v.IsOfficial
            && v.CanPlayInApp
            && Formax.Domain.Constants.MatchVideoVerificationStatuses.IsShowable(v.VerificationStatus)
            && string.IsNullOrWhiteSpace(v.RejectionReason)
            && !string.IsNullOrWhiteSpace(v.EmbedUrl);

        /// <summary>
        /// Ekranda (oynatılamasa bile) GÖRÜNEBİLİR mi? Doğrulanmış ama gömmeye kapalı
        /// resmî kaynak kullanıcıya "resmî kaynakta izle" olarak sunulabilir; yanlış ya
        /// da şüpheli kayıt HİÇ görünmez.
        /// </summary>
        public static bool IsVisible(MatchVideo v)
            => v is not null
            && HasOfficialWebEvidence(v)
            && v.IsOfficial
            && string.IsNullOrWhiteSpace(v.RejectionReason)
            && v.VerificationStatus is
                   Formax.Domain.Constants.MatchVideoVerificationStatuses.Verified
                or Formax.Domain.Constants.MatchVideoVerificationStatuses.EmbedBlocked;
    }
}
