using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Intelligence.Scenarios
{
    /// <summary>
    /// FORMAX Radar v2.2 — bir senaryonun ait olduğu market ailesi. Ranking aşamasında
    /// her aileden EN FAZLA bir senaryo seçilir → çelişen/tekrar eden marketler elenir
    /// (ör. "2.5 Üst" ile "2.5 Alt" aynı ailede; ikisi birden seçilemez).
    /// </summary>
    public enum ScenarioFamily
    {
        Outcome = 0,   // 1X2, çifte şans, kaybetmez
        Totals = 1,    // 0.5/1.5/2.5/3.5 alt-üst
        Btts = 2,      // KG Var / KG Yok
        Half = 3,      // ilk yarı marketleri
        TeamGoals = 4  // takım gol atar / atamaz
    }

    /// <summary>
    /// Deterministik market değerlendirme çıktısı. Probability + Confidence backend'de
    /// hesaplanır; EvidenceTags LLM'in "neden öne çıkıyor"u gerekçelendirmesini sağlar.
    /// LLM bu değerleri DEĞİŞTİRMEZ.
    /// </summary>
    public sealed class ScenarioCandidate
    {
        public string Market { get; set; } = "";
        public int Probability { get; set; }           // 0–100, deterministik
        public string Confidence { get; set; } = "";   // YÜKSEK | ORTA | DÜŞÜK
        public ScenarioFamily Family { get; set; }
        public double Weight { get; set; } = 1.0;       // market "anlamlılık" ağırlığı (ranking)
        public List<string> EvidenceTags { get; set; } = new();

        /// <summary>Ranking skoru: olasılık × anlamlılık ağırlığı.</summary>
        public double Score => Probability * Weight;
    }
}
