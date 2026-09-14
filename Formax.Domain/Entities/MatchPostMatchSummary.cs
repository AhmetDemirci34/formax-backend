using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// BİTMİŞ MAÇIN KISA ANALİZ METNİ — arka planda, yalnız DB'deki doğrulanmış veriden.
    ///
    /// Maç öncesi AI analizi (<see cref="MatchAnalysisSnapshot"/>) bu tabloya YAZILMAZ ve
    /// bu metin maç öncesi yorumdan TÜRETİLMEZ. Girdi: kayıtlı skor, devre skoru, kanonik
    /// olaylar (gol/kart) ve varsa takım istatistikleri. Kullanıcı sayfası yalnız bu satırı
    /// okur; sayfa açılışı LLM ya da dış kaynak isteği üretmez.
    /// </summary>
    public sealed class MatchPostMatchSummary
    {
        public int Id { get; set; }

        /// <summary>Kanonik Match.Id — maç başına tek satır.</summary>
        public int MatchId { get; set; }

        /// <summary>Metni üreten girdilerin SHA-256 özeti; girdi değişmedikçe yeniden yazılmaz.</summary>
        public string InputHash { get; set; } = string.Empty;

        /// <summary>2–4 kısa cümle, satır sonuyla ayrılmış.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Metinde kullanılan olguların JSON dökümü (denetim için).</summary>
        public string EvidenceJson { get; set; } = string.Empty;

        /// <summary>"Deterministic" — LLM kullanılmadı.</summary>
        public string Generator { get; set; } = "Deterministic";

        public int LlmCalls { get; set; }

        public DateTime GeneratedAtUtc { get; set; }
    }
}
