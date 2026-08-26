using System.Collections.Generic;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Match Archetype Engine (DNA Evolution).
    ///
    /// Match DNA boyutlarından maçın KARAKTER arketiplerini sınıflandırır (Açık/Kontrollü/Tempolu/
    /// Sabırlı/Fiziksel/Kontratak/Kaotik/Dengeli). Yalnız DNA'dan deterministik türetilir. "Kanat
    /// ağırlıklı" ve "Duran top ağırlıklı" için gerçek veri (kanat/duran-top istatistiği) context'te
    /// OLMADIĞINDAN üretilmez (fake YOK — dürüst). Stateless.
    /// </summary>
    internal sealed class MatchArchetypeEngine
    {
        public IReadOnlyList<string> Classify(MatchDna dna)
        {
            var tags = new List<string>();

            if (dna.Openness.Score >= 60) tags.Add("Açık maç");
            if (dna.Openness.Score < 40 && dna.Tempo.Score < 45) tags.Add("Kontrollü maç");
            if (dna.Tempo.Score >= 62) tags.Add("Tempolu maç");
            if (dna.Tempo.Score < 40 && dna.GoalPotential.Score < 45) tags.Add("Sabırlı maç");
            if (dna.PhysicalIntensity.Score >= 60) tags.Add("Fiziksel maç");
            if (dna.CounterAttack.Score >= 60) tags.Add("Kontratak ağırlıklı");
            if (dna.ChaosRisk.Score >= 55) tags.Add("Kaotik maç");
            if (dna.Balance.Score >= 66) tags.Add("Dengeli maç");

            // Hiç net arketip yoksa baskın tempo/açıklık yönünü ver (dürüst tek etiket).
            if (tags.Count == 0)
                tags.Add(dna.Tempo.Score >= 50 ? "Tempolu maç" : "Kontrollü maç");

            return tags;
        }
    }
}
