using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Tactical Intelligence.
    ///
    /// Takım oyun karakterini GERÇEK veriden türetir. Bugün context'te yalnız gol-üretim/yenilen-gol
    /// indeksleri (hücum-savunma eğilimi) ve beklenen gol (tempo) mevcut → bunlar üretilir. Pres,
    /// topa sahip olma, kanat kullanımı, duran top, savunma çizgisi verisi context'te OLMADIĞINDAN
    /// üretilmez; UnavailableAspects'te DÜRÜSTÇE listelenir (fake YOK). Coverage yoksa HasData=false.
    /// Stateless & deterministik.
    /// </summary>
    internal sealed class TacticalEngine
    {
        public TacticalProfile Analyze(UnifiedMatchAiContext ctx, PoissonGoalModel model)
        {
            var s = ctx.Strength;
            var has = (s.HomeAttackIndex + s.AwayAttackIndex + s.HomeDefenceIndex + s.AwayDefenceIndex) > 0;
            if (!has)
                return new TacticalProfile
                {
                    HasData = false,
                    UnavailableAspects = AllUnavailable()
                };

            // Hücum-savunma eğilimi: net hücum (attack - defence) ortalaması, -1..+1'e normalize.
            var homeNet = s.HomeAttackIndex - s.HomeDefenceIndex;
            var awayNet = s.AwayAttackIndex - s.AwayDefenceIndex;
            var tilt = Math.Clamp((homeNet + awayNet) / 4.0, -1, 1);

            var totalExp = model.ExpHome + model.ExpAway;
            var tempo = (int)Math.Clamp(totalExp / 4.0 * 100, 0, 100);

            return new TacticalProfile
            {
                HasData = true,
                AttackingTilt = Math.Round(tilt, 3),
                TempoLean = tempo,
                UnavailableAspects = new List<string>
                {
                    "Pres (veri yok)", "Topa sahip olma (veri yok)", "Kanat kullanımı (veri yok)",
                    "Duran top (veri yok)", "Savunma çizgisi (veri yok)"
                },
                Summary = $"Taktik — hücum eğilimi {tilt:+0.00;-0.00}, tempo {tempo}/100 (gol indekslerinden). " +
                          "Pres/topa sahip olma/kanat/duran top/savunma çizgisi verisi kapsam dışı."
            };
        }

        private static IReadOnlyList<string> AllUnavailable() => new List<string>
        {
            "Hücum-savunma eğilimi (gol verisi yok)", "Pres (veri yok)", "Topa sahip olma (veri yok)",
            "Kanat kullanımı (veri yok)", "Duran top (veri yok)", "Savunma çizgisi (veri yok)"
        };
    }
}
