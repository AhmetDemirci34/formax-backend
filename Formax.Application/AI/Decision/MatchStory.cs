using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// Football AI Brain v3 — STORY ENGINE çıktısı. Editorial Reasoning + tüm Intelligence katmanlarını
    /// TEK hikâyeye indirger. YALNIZ mevcut AiDecisionPackage bileşenlerinden (Editorial/FootballIntelligence/
    /// Importance/Surprise/Context) türetilir; olasılık/gol modeline DOKUNMAZ (hash sabit). Veri yoksa boş.
    /// LLM YALNIZ bunu okuyup doğal Türkçeye çevirir; yeni analiz/tahmin üretmez.
    /// </summary>
    public sealed class MatchStory
    {
        public bool HasData { get; init; }

        /// <summary>Maçın ANA hikâyesi (en kritik konu).</summary>
        public string MainStory { get; init; } = "";
        /// <summary>YAN hikâye (ikincil belirleyici tema).</summary>
        public string SubStory { get; init; } = "";
        /// <summary>Kırılma noktası — maçın seyrini değiştirebilecek unsur.</summary>
        public string TurningPoint { get; init; } = "";
        /// <summary>En büyük avantaj (gerekçeli).</summary>
        public string BiggestAdvantage { get; init; } = "";
        /// <summary>En büyük risk / belirsizlik.</summary>
        public string BiggestRisk { get; init; } = "";
        /// <summary>Sürpriz ihtimali (kesinlik DEĞİL; gerekçeli değerlendirme).</summary>
        public string SurprisePotential { get; init; } = "";
        /// <summary>FORMAX'ın editoryal görüşü (neden izlenmeli / genel değerlendirme).</summary>
        public string FormaxView { get; init; } = "";

        /// <summary>News Intelligence — haberin YORUMU (implikasyon), ham haber DEĞİL. "kulüp açıkladı/habere
        /// göre" gibi kaynak dili yasaktır; yalnız "→ etkileyebilir" tarzı çıkarım.</summary>
        public IReadOnlyList<string> NewsImplications { get; init; } = new List<string>();
    }
}
