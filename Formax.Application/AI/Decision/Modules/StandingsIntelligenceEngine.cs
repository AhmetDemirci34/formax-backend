using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2 MODÜL — Standings Intelligence Engine.
    ///
    /// Puan tablosunu OKUMAKLA kalmaz, göreli bağlamı üretir: kim üst sırada, puan/averaj farkı,
    /// form. Yalnız gerçek <see cref="StandingsAiSignals"/>'ten türetir. Lig büyüklüğü context'te
    /// olmadığından zone (Avrupa/küme) ve şampiyonluk% ÜRETİLMEZ (fake YOK; dürüst kapsam).
    /// Not: ham sıralama gücü zaten AiSignalFactory'de StandingsStrength sinyalidir → bu modül
    /// yön/edge'e ENJEKTE ETMEZ (çift sayım önlenir); yorum + motivasyon + açıklama üretir. Stateless.
    /// </summary>
    internal sealed class StandingsIntelligenceEngine
    {
        public StandingsInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var st = ctx.Standings;
            if (st == null || !st.HasData || st.Home.Position <= 0 || st.Away.Position <= 0)
                return new StandingsInsight { HasData = false };

            var h = st.Home; var a = st.Away;
            var pointGap = h.Points - a.Points;
            var gdGap = h.GoalDifference - a.GoalDifference;
            var higher = h.Position < a.Position ? "Home" : a.Position < h.Position ? "Away" : "Level";

            // Göreli eğilim: pozisyon farkı + puan farkı (bağlam yorumu; edge'e enjekte edilmez).
            var posLean = Math.Clamp((a.Position - h.Position) / 10.0, -1, 1); // ev daha üst → +
            var ptsLean = Math.Clamp(pointGap / 15.0, -1, 1);
            var lean = Math.Clamp(posLean * 0.5 + ptsLean * 0.5, -1, 1);

            // Güven: sıralama güvenilir; oynanan maç arttıkça artar.
            var played = Math.Min(h.Played, a.Played);
            var confidence = (int)Math.Clamp(55 + played * 1.5, 55, 90);

            var summary =
                $"Ev {h.Position}. ({h.Points}p, AV {h.GoalDifference:+0;-0;0}) / " +
                $"Dep {a.Position}. ({a.Points}p, AV {a.GoalDifference:+0;-0;0}); " +
                (higher == "Level" ? "sıralama başa baş" :
                 $"{(higher == "Home" ? ctx.HomeName : ctx.AwayName)} üst sırada, puan farkı {Math.Abs(pointGap)}") + ".";

            // v2.5 — tam tablo bağlamı (lider farkı / title-race) StandingsContext bloğundan.
            var sc = ctx.StandingsContext;
            var hasCtx = sc != null && sc.HasData;

            // v3 — bölge sınıflandırması: yalnız TAM tablo mevcutsa (küme hattı bilinir); aksi halde dürüst.
            var fullTable = hasCtx && sc.RelegationDataAvailable;
            var size = hasCtx ? sc.KnownRows : 0;
            var homeZone = ClassifyZone(h.Position, size, fullTable);
            var awayZone = ClassifyZone(a.Position, size, fullTable);

            return new StandingsInsight
            {
                HasData = true,
                HomePosition = h.Position,
                AwayPosition = a.Position,
                HomePoints = h.Points,
                AwayPoints = a.Points,
                PointGap = pointGap,
                GoalDiffGap = gdGap,
                HigherPlacedSide = higher,
                Lean = Math.Round(lean, 3),
                Confidence = confidence,
                Summary = summary + (hasCtx ? " " + sc.Summary : ""),
                LeaderPoints = hasCtx ? sc.LeaderPoints : 0,
                LeaderName = hasCtx ? sc.LeaderName : "",
                HomeGapToLeader = hasCtx ? sc.HomeGapToLeader : -1,
                AwayGapToLeader = hasCtx ? sc.AwayGapToLeader : -1,
                TitleRace = hasCtx && sc.TitleRace,
                RelegationDataAvailable = hasCtx && sc.RelegationDataAvailable,
                HomeZone = homeZone,
                AwayZone = awayZone
            };
        }

        /// <summary>
        /// Pozisyonu bölgeye çevirir — YALNIZ tam tablo mevcutsa (küme hattı bilinir). Kısmi kapsamda
        /// "Kısmi kapsam" döner (fake zone YOK). Eşikler lig büyüklüğüne oranlıdır (hardcode lig yok).
        /// </summary>
        private static string ClassifyZone(int position, int size, bool fullTable)
        {
            if (position <= 0) return "";
            if (!fullTable || size < 6) return "Kısmi kapsam";
            var topBand = System.Math.Max(1, (int)System.Math.Round(size * 0.15));
            var upperBand = System.Math.Max(topBand + 1, (int)System.Math.Round(size * 0.35));
            var relBand = System.Math.Max(1, (int)System.Math.Round(size * 0.15));
            if (position <= topBand) return "Şampiyonluk/Avrupa hattı";
            if (position <= upperBand) return "Üst sıra";
            if (position > size - relBand) return "Küme düşme hattı";
            if (position > (int)System.Math.Round(size * 0.65)) return "Alt sıra";
            return "Orta sıra";
        }
    }
}
