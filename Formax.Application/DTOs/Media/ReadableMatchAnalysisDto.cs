using Formax.Application.Common.Enums;
using Formax.Application.DTOs.AI;
using Formax.Application.DTOs.Common;

namespace Formax.Application.DTOs.Media
{
    /// <summary>
    /// Okunabilir, medya ve bilgilendirme amaçlı AI maç analizi DTO'su.
    /// State-machine ve AI etik kuralları tarafından kontrol edilir.
    /// </summary>
    public class ReadableMatchAnalysisDto
    {
        /// <summary>
        /// Analizi yapılan maçın ID'si
        /// </summary>
        public int MatchId { get; set; }

        /// <summary>
        /// AI konuşmadığında veya geri çekildiğinde gösterilen uyarı
        /// </summary>
        public string? Warning { get; set; }

        /// <summary>
        /// State-machine kontrollü ana AI mesajı
        /// </summary>
        public string? MainMessage { get; set; }

        /// <summary>
        /// Kural / state tabanlı anlatı (LLM DEĞİL)
        /// </summary>
        public AINarrativeDto? AiNarrative { get; set; }

        /// <summary>
        /// UI / medya başlığı
        /// </summary>
        public string? Headline { get; set; }

        /// <summary>
        /// FAZ 6 — LLM veya mock LLM tarafından üretilen özet bağlam metni
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Öne çıkan maddeler (ileride LLM veya rule-based üretilebilir)
        /// </summary>
        public string? KeyPoints { get; set; }

        /// <summary>
        /// AI içerik seviyesi (Free / Pro)
        /// </summary>
        public AITier AITier { get; set; }

        /// <summary>
        /// AI state bilgisi ve karar meta verisi
        /// </summary>
        public AIStateMetaDto? AIStateMeta { get; set; }

        /// <summary>
        /// AI'nin neden bu şekilde davrandığını açıklayan metin
        /// </summary>
        public AIExplanationDto? AIExplanation { get; set; }

        /// <summary>
        /// İçerik erişim ve hukuki bilgilendirme
        /// </summary>
        public ContentAccessDto? ContentAccess { get; set; }

        /// <summary>
        /// Kullanıcı koruma ve sorumluluk bilgileri
        /// </summary>
        public UserProtectionDto? UserProtection { get; set; }
    }
}
