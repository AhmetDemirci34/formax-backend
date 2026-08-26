using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Match Importance Engine.
    ///
    /// Maçın önemini TEK skorda toplar: Competition + Standings + Derby + Motivation + Tournament +
    /// Season + Pressure bileşenlerinden. YALNIZ HasData olan bileşen katkı verir (fake YOK). Skor,
    /// aktif bileşenlerin ortalaması ile en yüksek bileşenin (peak) harmanıdır → tek güçlü bağlam
    /// (ör. eleme finali) maçı kritik yapar. Stateless & deterministik.
    /// </summary>
    internal sealed class MatchImportanceEngine
    {
        public MatchImportance Compute(ContextIntelligence ci)
        {
            var comps = new List<ImportanceComponent>();

            if (ci.Competition.HasData && ci.Competition.StakeLevel > 0)
                comps.Add(Comp("Müsabaka", (int)(ci.Competition.StakeLevel * 100),
                    $"{ci.Competition.Importance} ({ci.Competition.CompetitionType})"));

            if (ci.Tournament.HasData && (ci.Tournament.PenaltiesPossible || ci.Tournament.AggregateMatters))
                comps.Add(Comp("Turnuva", 70, ci.Tournament.Summary));

            if (ci.Standings.HasData)
            {
                var st = ci.Standings;
                var contribution = st.TitleRace ? 85
                    : st.HomeGapToLeader >= 0 || st.AwayGapToLeader >= 0
                        ? Math.Clamp(60 - MinNonNeg(st.HomeGapToLeader, st.AwayGapToLeader), 20, 60)
                        : 30;
                comps.Add(Comp("Sıralama", contribution, st.TitleRace ? "şampiyonluk yarışı" : "sıralama bağlamı"));
            }

            if (ci.Motivation.HasData)
            {
                var m = Math.Max(ci.Motivation.HomeMotivation, ci.Motivation.AwayMotivation);
                comps.Add(Comp("Motivasyon", Math.Clamp((m - 50) * 2, 0, 100), ci.Motivation.Summary));
            }

            if (ci.Derby.HasData)
                comps.Add(Comp("Derbi", (int)(ci.Derby.Intensity * 100), "derbi atmosferi"));

            if (ci.Season.HasData && ci.Season.EndStake > 0)
                comps.Add(Comp("Sezon", (int)(ci.Season.EndStake * 100), $"faz {ci.Season.SeasonPhase}"));

            if (ci.Pressure.HasData)
                comps.Add(Comp("Baskı", ci.Pressure.OverallPressure, "psikolojik baskı"));

            if (comps.Count == 0)
                return new MatchImportance { Score = 0, Level = "DÜŞÜK", Summary = "Belirgin bağlam verisi yok." };

            var avg = comps.Average(c => c.Contribution);
            var peak = comps.Max(c => c.Contribution);
            var score = (int)Math.Clamp(Math.Round(avg * 0.5 + peak * 0.5), 0, 100);
            var level = score >= 75 ? "KRİTİK" : score >= 55 ? "YÜKSEK" : score >= 35 ? "ORTA" : "DÜŞÜK";

            var top = comps.OrderByDescending(c => c.Contribution).First();
            return new MatchImportance
            {
                Score = score,
                Level = level,
                Components = comps.OrderByDescending(c => c.Contribution).ToList(),
                Summary = $"Önem {score}/100 ({level}); en güçlü etken: {top.Name} ({top.Detail})."
            };
        }

        private static ImportanceComponent Comp(string name, int contribution, string detail) => new()
        {
            Name = name, Contribution = Math.Clamp(contribution, 0, 100), Detail = detail
        };

        private static int MinNonNeg(int a, int b)
        {
            var vals = new[] { a, b }.Where(v => v >= 0).ToList();
            return vals.Count == 0 ? 99 : vals.Min();
        }
    }
}
