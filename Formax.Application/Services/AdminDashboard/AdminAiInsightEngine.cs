using System.Collections.Generic;
using Formax.Application.DTOs.Admin;

namespace Formax.Application.Services.AdminDashboard
{
    /// <summary>
    /// Admin dashboard metriklerini yorumlayarak
    /// insan okunur içgörüler üretir.
    /// UI ve Controller bağı yoktur.
    /// </summary>
    public class AdminAiInsightEngine
    {
        public IReadOnlyList<string> BuildInsights(AdminAiTimelineDto timeline)
        {
            var insights = new List<string>();

            var last30 = timeline.Last30Days;

            if (last30.Total == 0)
            {
                insights.Add("Bu dönemde AI herhangi bir karar üretmemiştir.");
                insights.Add("Bu durum sistemin bilinçli olarak sessiz kaldığını gösterir.");
                return insights;
            }

            var extendedRatio = (double)last30.Extended / last30.Total;
            var silentRatio = (double)last30.Silent / last30.Total;
            var selfRetractedRatio = (double)last30.SelfRetracted / last30.Total;

            if (extendedRatio > 0.7)
            {
                insights.Add("AI kararların büyük kısmında kullanıcıya konuşmayı tercih etmiştir.");
            }
            else
            {
                insights.Add("AI karar üretirken temkinli davranmış, her senaryoda konuşmamıştır.");
            }

            if (silentRatio > 0.2)
            {
                insights.Add("Bazı senaryolarda AI, yeterli bağlam olmadığı için bilinçli olarak sessiz kalmıştır.");
            }

            if (selfRetractedRatio > 0)
            {
                insights.Add("Bazı kararlar çelişkili sinyaller nedeniyle AI tarafından geri çekilmiştir.");
            }
            else
            {
                insights.Add("Bu dönemde AI, çelişkili sinyal algılamamış ve geri çekilme yaşamamıştır.");
            }

            insights.Add(
                "Genel olarak AI davranışı; konuşma, susma ve geri çekilme arasında dengeli ve kontrollüdür.");

            return insights;
        }
    }
}
