using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Highlights
{
    /// <summary>
    /// ÖNEMLİ ANLAR — "Önemli Anları İzle" panelinin veri sözleşmesi.
    ///
    /// AI MAÇ ANALİZİ İLE İLİŞKİSİ YOKTUR (ayrı uç, ayrı zincir, ayrı içerik).
    ///
    /// İKİ AYRI GERÇEK VERİ:
    ///  • <see cref="Moments"/> — maçta GERÇEKTEN olan olaylar (gol/kart/penaltı),
    ///    FORMAX'ın kendi maç olayı deposundan. Video değildir, metin listesidir.
    ///  • <see cref="Videos"/> — maça KATI şekilde eşleşen resmi video içerikleri.
    ///
    /// TELİF: video hiçbir koşulda indirilmez/yeniden yayınlanmaz. Yalnız platformun
    /// kendi izin verdiği embed adresi taşınır (<see cref="MatchHighlightVideoDto.EmbedUrl"/>).
    /// Embed edilemeyen kaynak için EmbedUrl null kalır ve UI dürüst durum gösterir.
    /// </summary>
    public sealed class MatchHighlightsDto
    {
        public int MatchId { get; set; }

        /// <summary>"NotStarted" | "Live" | "Finished".</summary>
        public string State { get; set; } = "NotStarted";

        /// <summary>
        /// "NotStartedYet" = maç başlamadı, içerik aranmaz.
        /// "Ready"         = en az bir gerçek an/video bulundu.
        /// "NoContent"     = arama yapıldı, doğrulanmış içerik YOK (uydurulmaz).
        /// </summary>
        public string Status { get; set; } = "NotStartedYet";

        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        public List<MatchHighlightMomentDto> Moments { get; set; } = new();
        public List<MatchHighlightVideoDto> Videos { get; set; } = new();
    }

    public sealed class MatchHighlightMomentDto
    {
        public int Minute { get; set; }

        /// <summary>
        /// Ekranda gösterilecek dakika etiketi. Sağlayıcı penaltı atışlarını 120+ dakika
        /// olarak yazar; bunlar maçın 121. dakikası DEĞİLDİR → "PEN" gösterilir.
        /// </summary>
        public string MinuteLabel { get; set; } = string.Empty;
        /// <summary>"Goal" | "Card" | "Penalty" | "Subst" | "Var" …(sağlayıcı türü).</summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>Kullanıcıya gösterilecek Türkçe etiket ("Gol", "Kırmızı Kart"…).</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Kısa açıklama — YALNIZ sağlayıcının kendi alanlarından kurulur (takım, oyuncu,
        /// olay ayrıntısı). "Fenerbahçe öne geçti" gibi ÇIKARIM YAPILMAZ; skor durumu
        /// yorumlanmaz.
        /// </summary>
        public string? Description { get; set; }

        public string? Team { get; set; }
        public string? Player { get; set; }

        /// <summary>
        /// Bu ana ait doğrulanmış video (varsa) — video başlığında AÇIKÇA aynı dakika
        /// yazıyorsa bağlanır. Dakika eşleşmesi yoksa null; video UYDURULMAZ.
        /// </summary>
        public string? VideoId { get; set; }
    }

    public sealed class MatchHighlightVideoDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Başlıkta AÇIKÇA geçen dakika; yoksa null (uydurulmaz).</summary>
        public int? Minute { get; set; }
        public string Platform { get; set; } = string.Empty;
        /// <summary>Kanal/hesap adı.</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>Kaynağın kendi sayfası (ikincil seçenek — otomatik yönlendirme YOK).</summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>Platformun izin verdiği embed adresi; embed edilemiyorsa null.</summary>
        public string? EmbedUrl { get; set; }
        /// <summary>Platformun kendi küçük görseli; yoksa null.</summary>
        public string? ThumbnailUrl { get; set; }
        public bool Embeddable { get; set; }
        public DateTime PublishedUtc { get; set; }
    }
}
