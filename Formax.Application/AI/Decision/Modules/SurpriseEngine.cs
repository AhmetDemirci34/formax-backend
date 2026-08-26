using System;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Surprise Engine.
    ///
    /// Favori olmasına rağmen ALIŞILMADIK derecede yüksek risk oluşuyorsa (yüksek sürpriz DNA'sı +
    /// çelişki) upset uyarısı üretir. Yalnız gerçek DNA/çelişki verisinden. Stateless & deterministik.
    /// </summary>
    internal sealed class SurpriseEngine
    {
        public SurpriseAlert Analyze(MatchDna dna, SignalField field, ContradictionReport contradiction)
        {
            var edge = field.NetHomeEdge;
            var hasFavorite = Math.Abs(edge) >= 0.20;
            var favored = !hasFavorite ? "None" : edge > 0 ? "Home" : "Away";

            // Sürpriz potansiyeli = DNA sürpriz + çelişki katkısı.
            var potential = (int)Math.Clamp(
                dna.SurprisePotential.Score + (contradiction.HasContradiction ? contradiction.Severity * 0.4 : 0), 0, 100);

            // Uyarı: net favori VAR ve sürpriz potansiyeli yüksek.
            var alert = hasFavorite && potential >= 55;

            return new SurpriseAlert
            {
                HasAlert = alert,
                Potential = potential,
                FavoredSide = favored,
                Summary = alert
                    ? $"UPSET UYARISI: {(favored == "Home" ? "ev" : "deplasman")} favori ama sürpriz potansiyeli {potential}/100."
                    : hasFavorite ? $"Favori ({(favored == "Home" ? "ev" : "deplasman")}) için sürpriz riski düşük ({potential})."
                                  : "Net favori yok; sürpriz değerlendirmesi uygulanmadı."
            };
        }
    }
}
