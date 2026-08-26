using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Live
{
    /// <summary>
    /// CANLI TAKİP — "Maçta ne oluyor?" sorusunun cevabı: KANONİK MAÇ OLAYLARI.
    ///
    /// BU EKRAN VİDEO ARAMAZ. Video "Önemli Anları İzle" özelliğinin işidir (ayrı uç,
    /// ayrı hat). Burada LLM/Gemma da yoktur; olaylar deterministik olarak çıkarılır.
    ///
    /// AI MAÇ ANALİZİ İLE İLİŞKİSİ YOKTUR: Decision/Narrative/Voice zinciri çağrılmaz.
    ///
    /// KAYNAK: yalnız global kaynaklar (haber deposu + resmi sosyal paylaşımlar).
    /// api-football canlı akışı bu ekranın kaynağı DEĞİLDİR.
    /// </summary>
    public sealed class MatchLiveFeedDto
    {
        public int MatchId { get; set; }

        /// <summary>"NotStarted" | "Live" | "Finished" | "Unknown".</summary>
        public string State { get; set; } = "NotStarted";

        /// <summary>Kullanıcıya gösterilecek dürüst durum cümlesi.</summary>
        public string? StateMessage { get; set; }

        /// <summary>
        /// "CANLI" etiketi ancak bu true iken gösterilir: maçın şu an oynandığı GLOBAL
        /// kaynaktan gelen TAZE bir olayla doğrulandı. Zaman penceresi bunu true yapmaz.
        /// </summary>
        public bool LiveConfirmed { get; set; }

        public DateTime KickoffUtc { get; set; }

        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        /// <summary>
        /// Skor. Maç sürerken YALNIZ global kaynağın yazdığı skordan (SCORE_UPDATE olayı);
        /// maç bittiğinde kayıtlı kesin sonuçtan. Doğrulanamıyorsa null.
        /// </summary>
        public MatchLiveScoreDto? Score { get; set; }

        public bool ScoreIsFinal { get; set; }

        public string? ScoreUnavailableReason { get; set; }

        /// <summary>
        /// SKOR ÇELİŞKİSİ — kayıtlı skor maçın tamamını kapsamıyorsa (uzatma/penaltı
        /// sonrası güncellenmemişse) dürüst açıklama. Çelişki UI'da GİZLENMEZ.
        /// </summary>
        public string? ScoreNote { get; set; }

        /// <summary>Penaltı seri sonucu — yalnız seri oynandıysa dolu.</summary>
        public int? ShootoutHome { get; set; }
        public int? ShootoutAway { get; set; }

        /// <summary>Maç olayları — TERS KRONOLOJİK (en yeni en üstte).</summary>
        public List<MatchLiveEventItemDto> Events { get; set; } = new();
    }

    public sealed class MatchLiveScoreDto
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        /// <summary>Kayıtlı devre bilgisi (FT/AET/PEN); canlı skorda null.</summary>
        public string? Phase { get; set; }
        /// <summary>"record" = kayıtlı kesin sonuç, "global" = global kaynağın yazdığı skor.</summary>
        public string Origin { get; set; } = "record";
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Tek bir canlı maç olayı. Alanlar sözleşmesi:
    /// minute / eventType / team / player / description / source / publishedAt.
    /// </summary>
    public sealed class MatchLiveEventItemDto
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Kanonik tür (GOAL, RED_CARD, HALF_TIME…). Bkz. MatchEventExtractor.</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Kullanıcıya gösterilecek Türkçe etiket ("GOL", "KIRMIZI KART").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Dakika — YALNIZ kaynak metninde açıkça yazıyorsa. Yayın saatinden HESAPLANMAZ.
        /// </summary>
        public int? Minute { get; set; }
        public string? MinuteLabel { get; set; }

        /// <summary>Kaynak açıkça belirttiyse takım; aksi hâlde null.</summary>
        public string? Team { get; set; }

        /// <summary>Kaynak "Gol: Ad Soyad" biçiminde verdiyse oyuncu; aksi hâlde null.</summary>
        public string? Player { get; set; }

        /// <summary>Kaynağın kendi metni — FORMAX cümle üretmez.</summary>
        public string Description { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }

        /// <summary>"news" | "social".</summary>
        public string Origin { get; set; } = "news";
    }
}
