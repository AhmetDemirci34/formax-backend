using Formax.Application.DTOs.MatchIntelligence;
using Formax.Application.DTOs.Nabiz;

namespace Formax.Application.Services.MatchIntelligence.News;

public interface INewsIntelligenceEngine
{
    NewsIntelligenceDto Analyze(IReadOnlyList<NabizFeedItemDto> items, string homeTeam, string awayTeam);
}

/// <summary>
/// News Intelligence Engine (Görev #015). Ham haber akışını (MatchSocialFeed → NabizFeedItemDto)
/// sınıflandırır, önem puanlar, etki analizini yapar, hero seçer, AI özeti ve evidence üretir.
/// Frontend hiçbir analiz yapmaz. Haber yoksa graceful boş sonuç (uydurma yok).
/// </summary>
public sealed class NewsIntelligenceEngine : INewsIntelligenceEngine
{
    public NewsIntelligenceDto Analyze(IReadOnlyList<NabizFeedItemDto> items, string homeTeam, string awayTeam)
    {
        var now = DateTime.UtcNow;

        if (items == null || items.Count == 0)
        {
            return new NewsIntelligenceDto
            {
                HomeTeam = homeTeam, AwayTeam = awayTeam,
                Hero = false, Confidence = 0, ImportanceScore = 0, Impact = "",
                AiSummary = "Bu maç için öne çıkan güncel gelişme bulunmuyor.",
                Evidence = new() { new() { Label = "Durum", Detail = "Güncel gelişme yok" } }
            };
        }

        // Sınıflandır + puanla, önem sırasına diz.
        var scored = items
            .Select((it, idx) =>
            {
                var type = NewsImportanceAnalyzer.Classify(it.Headline, it.Summary ?? "", it.Type);
                var score = NewsImportanceAnalyzer.Score(type, it.Headline, it.PublishedAt, it.AuthorVerified, it.Source, now);
                return new Scored(it, idx, type, score,
                    NewsImpactAnalyzer.Analyze(type, it.Headline),
                    TeamOf(it.Headline, it.Summary ?? "", homeTeam, awayTeam));
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var articles = scored.Select((x, rank) => new NewsArticleIntelDto
        {
            Id = $"n{x.Index}",
            Title = x.Item.Headline,
            Summary = x.Item.Summary ?? "",
            Source = x.Item.Source,
            Time = RelativeTime(x.Item.PublishedAt, now),
            Type = x.Type,
            Importance = NewsImportanceAnalyzer.Label(x.Score),
            ImportanceScore = x.Score,
            Team = x.Team,
            Impact = x.Impact,
            Hero = rank == 0, // en yüksek önem puanlı = Hero
        }).ToList();

        var hero = articles[0];
        var confidence = Math.Clamp(35 + Math.Min(45, articles.Count * 8), 0, 95);

        return new NewsIntelligenceDto
        {
            HomeTeam = homeTeam, AwayTeam = awayTeam,
            HeroNews = hero,
            Articles = articles,
            AiSummary = NewsSummaryGenerator.Generate(hero, articles),
            Evidence = NewsEvidenceBuilder.Build(hero, articles),
            ImportanceScore = hero.ImportanceScore,
            Impact = hero.Impact,
            Hero = true,
            Confidence = confidence,
        };
    }

    private static string TeamOf(string headline, string summary, string home, string away)
    {
        var text = (headline + " " + summary).ToLowerInvariant();
        var h = FirstToken(home);
        var a = FirstToken(away);
        var inHome = !string.IsNullOrEmpty(h) && text.Contains(h);
        var inAway = !string.IsNullOrEmpty(a) && text.Contains(a);
        if (inHome && !inAway) return "home";
        if (inAway && !inHome) return "away";
        return "general";
    }

    private static string FirstToken(string name)
        => string.IsNullOrWhiteSpace(name) ? "" : name.Split(' ')[0].ToLowerInvariant();

    private static string RelativeTime(DateTime publishedAtUtc, DateTime nowUtc)
    {
        var span = nowUtc - publishedAtUtc;
        if (span.TotalMinutes < 1) return "az önce";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} dk önce";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} saat önce";
        return $"{(int)span.TotalDays} gün önce";
    }

    private sealed record Scored(
        NabizFeedItemDto Item, int Index, string Type, int Score, string Impact, string Team);
}
