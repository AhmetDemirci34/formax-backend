using Formax.Application.DTOs.Home;

namespace Formax.Application.Services.Recommendation;

public class RecommendationExplainabilityService
{
    public List<string> BuildReasons(HomeRadarMatchDto match)
    {
        var reasons = new List<string>();

        if (match.Sapma >= 50)
            reasons.Add("Bu maçta beklenen tablo farklı gelişebilir.");

        if (match.MatchHeatScore >= 55)
            reasons.Add("Bugün bu eşleşmeye olan ilgi dikkat çekiyor.");

        if (match.TeamInterestScore >= 40)
            reasons.Add("Takımlar kullanıcıların ilgisini çekmeye devam ediyor.");

        if (match.LeagueInterestScore >= 40)
            reasons.Add("Bu lig bugün radarın dikkat çektiği liglerden biri.");

        if (match.BehaviorMomentumScore >= 60)
            reasons.Add("Bugün radarda giderek daha fazla dikkat çekiyor.");

        if (match.TimeProximityScore >= 80)
            reasons.Add("Başlangıç saati yaklaşırken radar bu maçı öne çıkarıyor.");

        // 🔒 FORMAX RULE
        // tek sebep gösterilmez

        if (reasons.Count == 1)
        {
            reasons.Insert(0, "Radar bugün bu eşleşmeyi dikkat çekici buldu.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Radar bugün bu eşleşmeyi izlemeye değer görüyor.");
            reasons.Add("Bu eşleşme radarın dikkatini çeken maçlardan biri.");
        }

        return reasons;
    }
}