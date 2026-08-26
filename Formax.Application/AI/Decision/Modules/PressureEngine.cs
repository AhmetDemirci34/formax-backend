using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2 MODÜL — Pressure Engine.
    ///
    /// GDP News ve Social sinyallerinden maçın psikolojik BASKI skorunu üretir: teknik direktör
    /// haberleri, resmi kulüp açıklamaları, breaking gelişmeler. Yalnız gerçek kanıt/resmi kaynak;
    /// dedikodu ingest'te elenmiştir. Haber/sosyal maç DÜZEYİNDE olduğundan takım ayrımı yapılmaz
    /// (dürüst: OverallPressure). Veri yoksa HasData=false. Stateless & deterministik.
    /// </summary>
    internal sealed class PressureEngine
    {
        public PressureInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var news = ctx.News;
            var social = ctx.Social;
            var hasNews = news != null && news.HasData;
            var hasSocial = social != null && social.HasData;
            if (!hasNews && !hasSocial)
                return new PressureInsight { HasData = false };

            double score = 0;

            if (hasNews)
            {
                score += Math.Min(30, news!.CoachNews * 12);           // TD baskısı
                score += Math.Min(25, news.OfficialAnnouncements * 10); // resmi/kulüp açıklaması
                score += Math.Min(15, news.ClubNews * 4);               // kulüp gündemi (transfer/schedule)
                if (news.HasBreakingNews) score += 15;
                // Kaynak güveniyle ölçekle (zayıf kaynak baskıyı şişirmesin).
                score *= Math.Clamp(news.SourceTrust / 90.0, 0.6, 1.0);
            }

            if (hasSocial)
            {
                score += Math.Min(20, social!.OfficialCoachStatement * 10);
                score += Math.Min(15, social.OfficialClubStatement * 6);
                if (social.BreakingOfficialNews) score += 10;
            }

            var overall = (int)Math.Clamp(Math.Round(score), 0, 100);
            if (overall <= 0)
                return new PressureInsight { HasData = false };

            var coach = hasNews ? news!.CoachNews : 0;
            var official = (hasNews ? news!.OfficialAnnouncements : 0) + (hasSocial ? social!.OfficialClubStatement : 0);
            var breaking = (hasNews && news!.HasBreakingNews) || (hasSocial && social!.BreakingOfficialNews);

            return new PressureInsight
            {
                HasData = true,
                OverallPressure = overall,
                Scope = "Match-level",
                Summary = $"Baskı {overall}/100 — TD haberi {coach}, resmi açıklama {official}, breaking {(breaking ? "var" : "yok")}."
            };
        }
    }
}
