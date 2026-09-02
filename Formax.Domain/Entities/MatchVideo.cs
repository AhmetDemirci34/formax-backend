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

        /// <summary>Doğrulamanın İNSAN OKUYABİLİR gerekçesi (teşhis; UI göstermez).</summary>
        public string VerificationNote { get; set; } = string.Empty;

        public DateTime VerifiedAtUtc { get; set; }
    }
}
