using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.News;

/// <summary>Tüm haberleri tek AI özetine indirger (Görev #015). Haberleri tek tek okumaz.</summary>
public static class NewsSummaryGenerator
{
    public static string Generate(NewsArticleIntelDto? hero, IReadOnlyList<NewsArticleIntelDto> all)
    {
        if (hero == null)
            return "Bu maç için öne çıkan güncel gelişme bulunmuyor.";

        var teamWord = hero.Team == "home" ? "ev sahibinde"
                     : hero.Team == "away" ? "deplasmanda"
                     : "gündemde";

        var lead = $"Maç öncesi en kritik gelişme {teamWord}: {hero.Type.ToLowerInvariant()} " +
                   $"({hero.Impact.ToLowerInvariant()} etkisi).";

        if (all.Count > 1)
        {
            var critical = all.Count(a => a.Importance == "critical");
            lead += $" Toplam {all.Count} gelişme AI önceliğine göre sıralandı" +
                    (critical > 1 ? $"; bunların {critical} tanesi kritik." : ".");
        }

        return lead;
    }
}
