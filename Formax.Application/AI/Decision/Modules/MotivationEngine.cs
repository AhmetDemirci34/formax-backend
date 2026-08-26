using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2 MODÜL — Motivation Engine.
    ///
    /// Takımların göreli motivasyon bağlamını üretir: üst sıradaki konumu koruma vs alttaki takımın
    /// yükselme/form baskısı. Göreli standings + form dizisinden türetilir. Zone/sezon/kalan-maç
    /// verisi context'te olmadığından "kesin küme düşme/şampiyonluk baskısı" gibi MUTLAK iddialar
    /// ÜRETİLMEZ (fake YOK; dürüst kısmi). Standings yoksa HasData=false. Stateless.
    /// </summary>
    internal sealed class MotivationEngine
    {
        // v2.5 — motivasyon artık standings + form + Competition önemi + Season fazından türetilir.
        public MotivationInsight Analyze(UnifiedMatchAiContext ctx, StandingsInsight standings,
            CompetitionInsight competition, SeasonInsight season)
        {
            // Taban: standings VEYA yüksek-stake bir müsabaka (eleme/kritik) olmalı; yoksa dürüstçe yok.
            var hasComp = competition != null && competition.HasData && competition.StakeLevel > 0;
            if (!standings.HasData && !hasComp)
                return new MotivationInsight { HasData = false };

            var st = ctx.Standings;
            var homeForm = FormRatio(st.Home.Form);
            var awayForm = FormRatio(st.Away.Form);
            var formDiff = homeForm.count > 0 && awayForm.count > 0
                ? Math.Clamp(homeForm.ratio - awayForm.ratio, -1, 1)
                : 0.0;

            // Göreli eğilim (standings varsa): sıralama (0.6) + form (0.4).
            var lean = standings.HasData
                ? Math.Clamp(standings.Lean * 0.6 + formDiff * 0.4, -1, 1)
                : 0.0;

            var homeMot = (int)Math.Clamp(50 + lean * 40, 10, 95);
            var awayMot = (int)Math.Clamp(50 - lean * 40, 10, 95);

            // Bağlam amplifikatörleri (yalnız gerçek veri): eleme/kritik → iki taraf da "kazanmalı";
            // sezon sonu → stake artar; şampiyonluk yarışı → yarıştaki takım motivasyonu yükselir.
            var stake = hasComp ? competition.StakeLevel : 0.0;
            var endStake = season != null && season.HasData ? season.EndStake : 0.0;
            var bump = (int)Math.Round(stake * 30 + endStake * 15);
            if (bump > 0) { homeMot = Math.Min(99, homeMot + bump); awayMot = Math.Min(99, awayMot + bump); }

            if (standings.HasData && standings.TitleRace)
            {
                // Lidere daha yakın (gap küçük) takım şampiyonluk baskısı taşır.
                if (standings.HomeGapToLeader >= 0 && (standings.AwayGapToLeader < 0 || standings.HomeGapToLeader <= standings.AwayGapToLeader))
                    homeMot = Math.Min(99, homeMot + 12);
                if (standings.AwayGapToLeader >= 0 && (standings.HomeGapToLeader < 0 || standings.AwayGapToLeader <= standings.HomeGapToLeader))
                    awayMot = Math.Min(99, awayMot + 12);
            }

            var ctxTag = BuildContextTag(competition, season, standings);

            return new MotivationInsight
            {
                HasData = true,
                HomeMotivation = homeMot,
                AwayMotivation = awayMot,
                Lean = Math.Round(lean, 3),
                HomeLabel = MotLabel(standings, true, homeForm.ratio, homeForm.count, competition, standings.TitleRace && standings.HomeGapToLeader >= 0),
                AwayLabel = MotLabel(standings, false, awayForm.ratio, awayForm.count, competition, standings.TitleRace && standings.AwayGapToLeader >= 0),
                Summary = $"Motivasyon — Ev {homeMot}/100, Dep {awayMot}/100" + (ctxTag.Length > 0 ? $" ({ctxTag})" : " (sıralama + form)") + "."
            };
        }

        private static string BuildContextTag(CompetitionInsight comp, SeasonInsight season, StandingsInsight st)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (comp != null && comp.HasData && comp.StakeLevel > 0) parts.Add(comp.IsElimination ? "eleme" : "yüksek önem");
            if (season != null && season.HasData && season.SeasonPhase == "End") parts.Add("sezon sonu");
            if (st.HasData && st.TitleRace) parts.Add("şampiyonluk yarışı");
            if (parts.Count == 0) parts.Add("sıralama + form");
            return string.Join(", ", parts);
        }

        // Dürüst, veri-kapsamlı etiket: yalnız türetilebileni söyle.
        private static string MotLabel(StandingsInsight st, bool isHome, double formRatio, int count,
            CompetitionInsight comp, bool inTitleRace)
        {
            var bits = new System.Collections.Generic.List<string>();
            if (comp != null && comp.HasData && comp.IsElimination) bits.Add("eleme - kazanmalı");
            if (inTitleRace) bits.Add("şampiyonluk yarışı");
            if (st.HasData)
                bits.Add((isHome ? st.HigherPlacedSide == "Home" : st.HigherPlacedSide == "Away") ? "üst sırayı koruma" : "sıralama baskısı");
            if (count > 0)
                bits.Add(formRatio >= 0.66 ? "form yüksek" : formRatio >= 0.4 ? "form dengeli" : "form düşük");
            return bits.Count > 0 ? string.Join(", ", bits) : "yeterli bağlam yok";
        }

        // Form dizisi (WWDLW) → (kazanma-oranı 0..1, adet).
        private static (double ratio, int count) FormRatio(string? form)
        {
            if (string.IsNullOrWhiteSpace(form)) return (0, 0);
            int pts = 0, n = 0;
            foreach (var ch in form)
            {
                var c = char.ToUpperInvariant(ch);
                if (c == 'W') { pts += 3; n++; }
                else if (c == 'D') { pts += 1; n++; }
                else if (c == 'L') { n++; }
            }
            return n == 0 ? (0, 0) : ((double)pts / (n * 3), n);
        }
    }
}
