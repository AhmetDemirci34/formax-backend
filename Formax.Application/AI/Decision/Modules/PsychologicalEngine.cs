using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Psychological Intelligence.
    ///
    /// GDP News + Social + Evidence sinyallerinden psikolojik profili üretir: baskı, teknik direktör
    /// durumu, kulüp krizi, transfer etkisi, resmi açıklamalar. Yalnız gerçek kanıt/resmi kaynak
    /// (dedikodu ingest'te elenmiş). Coverage yoksa HasData=false. Stateless & deterministik.
    /// </summary>
    internal sealed class PsychologicalEngine
    {
        public PsychologicalProfile Analyze(UnifiedMatchAiContext ctx, ContextIntelligence ci)
        {
            var news = ctx.News;
            var social = ctx.Social;
            var hasNews = news != null && news.HasData;
            var hasSocial = social != null && social.HasData;
            if (!hasNews && !hasSocial)
                return new PsychologicalProfile { HasData = false };

            var coach = (hasNews ? news!.CoachNews : 0) + (hasSocial ? social!.OfficialCoachStatement : 0);
            var transfer = (hasNews ? news!.TransferNews : 0) + (hasSocial ? social!.OfficialTransferAnnouncement : 0);
            var official = (hasNews ? news!.OfficialAnnouncements : 0) + (hasSocial ? social!.OfficialClubStatement : 0);
            var clubAgenda = (hasNews ? news!.ClubNews : 0) + official;
            var breaking = (hasNews && news!.HasBreakingNews) || (hasSocial && social!.BreakingOfficialNews);

            return new PsychologicalProfile
            {
                HasData           = true,
                PressureScore     = ci.Pressure.HasData ? ci.Pressure.OverallPressure : 0,
                CoachSituation    = (int)Math.Clamp(coach * 22, 0, 100),
                ClubCrisis        = (int)Math.Clamp(clubAgenda * 14 + (breaking ? 15 : 0), 0, 100),
                TransferImpact    = (int)Math.Clamp(transfer * 18, 0, 100),
                OfficialStatements = (hasNews && news!.HasOfficialSource) || (hasSocial && social!.HasOfficialSource),
                BreakingContext   = breaking,
                Summary           = $"Psikoloji — baskı {(ci.Pressure.HasData ? ci.Pressure.OverallPressure : 0)}, TD {coach}, transfer {transfer}, resmi {official}" +
                                    (breaking ? ", breaking gelişme." : ".")
            };
        }
    }
}
