namespace Formax.Application.Services.MatchIntelligence.News;

/// <summary>Haberin baskın etki alanını üretir (Görev #015).</summary>
public static class NewsImpactAnalyzer
{
    public static string Analyze(string type, string headline)
    {
        var h = (headline ?? "").ToLowerInvariant();

        if (h.Contains("savunma") || h.Contains("stoper") || h.Contains("defans") || h.Contains("bek "))
            return "Savunma";
        if (h.Contains("hücum") || h.Contains("forvet") || h.Contains("golcü") || h.Contains("kanat"))
            return "Hücum";
        if (h.Contains("orta saha"))
            return "Orta saha";

        return type switch
        {
            "Sakatlık"        => "Kadro derinliği",
            "Ceza"            => "Kadro derinliği",
            "İlk 11"          => "Taktik",
            "Teknik Direktör" => "Taktik",
            "Transfer"        => "Kadro derinliği",
            "Antrenman"       => "Genel",
            _                 => "Moral",
        };
    }
}
