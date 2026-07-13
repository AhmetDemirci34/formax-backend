using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.News;

/// <summary>Hero haberden evidence üretir: kaynak, yayın zamanı, haber tipi, etki alanı (Görev #015).</summary>
public static class NewsEvidenceBuilder
{
    public static List<NewsEvidenceItemDto> Build(NewsArticleIntelDto? hero, IReadOnlyList<NewsArticleIntelDto> all)
    {
        if (hero == null)
            return new() { new() { Label = "Durum", Detail = "Güncel gelişme yok" } };

        return new()
        {
            new() { Label = "En kritik gelişme", Detail = hero.Title },
            new() { Label = "Kaynak", Detail = hero.Source },
            new() { Label = "Yayın zamanı", Detail = hero.Time },
            new() { Label = "Haber tipi", Detail = hero.Type },
            new() { Label = "Etki alanı", Detail = hero.Impact },
        };
    }
}
