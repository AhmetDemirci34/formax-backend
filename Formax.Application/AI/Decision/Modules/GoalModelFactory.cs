using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// Beklenen-gol modeli fabrikası — Match DNA ve Probability motorlarının PAYLAŞTIĞI tek kaynak.
    ///
    /// Beklenen goller, kanıtlanmış FAZ 1-3 matematiğiyle (lig-uyarlı baseline + venue-özel gol
    /// ortalamaları + kadro cezası) türetilir; ÜZERİNE ağırlıklı sinyal alanının net yönü (edge)
    /// sınırlı bir tilt uygular → GDP zenginleştikçe model akıllanır, imza sabit kalır (OCP).
    /// Deterministik: aynı context + aynı alan → aynı model.
    /// </summary>
    internal static class GoalModelFactory
    {
        public static PoissonGoalModel Build(UnifiedMatchAiContext ctx, SignalField field,
            ContextIntelligence context = null)
        {
            var home = ctx.Home;
            var away = ctx.Away;

            // ── Kanıtlı beklenen-gol tabanı (MarketProbabilityEngine.Evaluate ile birebir uyumlu) ──
            var baseline = ctx.Strength.LeagueGoalBaseline;
            var fbHome = baseline > 0 ? Clamp(baseline * 1.09, 0.4, 2.6) : 1.2;
            var fbAway = baseline > 0 ? Clamp(baseline * 0.91, 0.4, 2.6) : 1.0;
            var expHome = Clamp(Avg(home.AvgGoalsFor, away.AvgGoalsAgainst, fbHome), 0.2, 3.5);
            var expAway = Clamp(Avg(away.AvgGoalsFor, home.AvgGoalsAgainst, fbAway), 0.2, 3.5);

            // Kadro uygunluğu (yalnız gerçek veri): eksikler ilgili takımın gol üretimini kısar.
            if (ctx.Availability.HasData)
            {
                var homePen = Math.Min(0.30, ctx.Availability.HomeKeyAbsences * 0.06);
                var awayPen = Math.Min(0.30, ctx.Availability.AwayKeyAbsences * 0.06);
                expHome = Clamp(expHome * (1.0 - homePen), 0.2, 3.5);
                expAway = Clamp(expAway * (1.0 - awayPen), 0.2, 3.5);
            }

            // ── Sinyal alanı tilt'i: net edge beklenen golleri sınırlı (±%15) kaydırır. ──
            // Edge=0 iken model DEĞİŞMEZ (geri-uyum); edge büyüdükçe favori lehine hafif asimetri.
            var edge = field.NetHomeEdge; // -1..+1
            expHome = Clamp(expHome * (1.0 + 0.15 * edge), 0.2, 3.6);
            expAway = Clamp(expAway * (1.0 - 0.15 * edge), 0.2, 3.6);

            // ── v2 Derbi etkisi (yalnız gerçek derbi sinyali varsa): derbiler tipik olarak daha
            // TEMKİNLİ ve DENGELİ geçer → beklenen golleri hafif kıs (≤%10) ve favori asimetrisini
            // ortalamaya doğru sıkıştır (≤%15). Derbi yoksa model DEĞİŞMEZ (backward-compat).
            if (context?.Derby?.HasData == true)
            {
                var k = Clamp(context.Derby.Intensity, 0, 1);
                var dampen = 1.0 - 0.10 * k;
                expHome *= dampen;
                expAway *= dampen;
                var mean = (expHome + expAway) / 2.0;
                expHome += (mean - expHome) * 0.15 * k;
                expAway += (mean - expAway) * 0.15 * k;
                expHome = Clamp(expHome, 0.2, 3.6);
                expAway = Clamp(expAway, 0.2, 3.6);
            }

            return new PoissonGoalModel(expHome, expAway);
        }

        private static double Avg(double a, double b, double fallback)
        {
            var v = (a + b) / 2.0;
            return v <= 0 ? fallback : v;
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
