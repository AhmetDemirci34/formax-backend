namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// News Intelligence Engine çıktısı (Görev #015).
//
// Ham haber akışı (MatchSocialFeed) sınıflandırılır, önem puanı verilir, etki analizi yapılır,
// hero seçilir, AI özeti ve evidence üretilir. Frontend yalnız render eder — sıralama/analiz burada.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class NewsIntelligenceDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";

    /// <summary>En yüksek önem puanlı haber (Hero).</summary>
    public NewsArticleIntelDto? HeroNews { get; init; }

    /// <summary>Tüm haberler, önem sırasına göre azalan (Hero dahil, Hero=true işaretli).</summary>
    public List<NewsArticleIntelDto> Articles { get; init; } = new();

    public string AiSummary { get; init; } = "";
    public List<NewsEvidenceItemDto> Evidence { get; init; } = new();

    /// <summary>Hero haberin önem puanı (0–100).</summary>
    public int ImportanceScore { get; init; }
    /// <summary>Hero haberin baskın etki alanı.</summary>
    public string Impact { get; init; } = "";
    /// <summary>Hero haber var mı?</summary>
    public bool Hero { get; init; }
    /// <summary>Analiz güveni (0–100) — haber hacmi + kaynak güvenilirliği.</summary>
    public int Confidence { get; init; }
}

public sealed class NewsArticleIntelDto
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Source { get; init; } = "";
    public string Time { get; init; } = "";
    /// <summary>Sakatlık | Ceza | İlk 11 | Teknik Direktör | Transfer | Basın Açıklaması | Antrenman | Kulüp Açıklaması</summary>
    public string Type { get; init; } = "";
    /// <summary>critical | high | medium | low</summary>
    public string Importance { get; init; } = "medium";
    public int ImportanceScore { get; init; }
    /// <summary>home | away | general</summary>
    public string Team { get; init; } = "general";
    /// <summary>Etki alanı (Savunma / Hücum / Kadro derinliği / Moral / Taktik / Genel).</summary>
    public string Impact { get; init; } = "";
    public bool Hero { get; init; }
}

public sealed class NewsEvidenceItemDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
