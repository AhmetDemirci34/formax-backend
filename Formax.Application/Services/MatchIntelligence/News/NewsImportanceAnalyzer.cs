namespace Formax.Application.Services.MatchIntelligence.News;

/// <summary>
/// Haber sınıflandırma + önem puanlama (Görev #015).
///
/// Sınıflandırma: SourceType ham "kaynak tipi" (Flash/Official/Roportaj…) haber KATEGORİSİ
/// vermediğinden, kategori başlık/özet anahtar kelimelerinden çıkarılır; SourceType ikincil sinyal.
///
/// Önem puanı = tip taban puanı + güncellik + kaynak güvenilirliği (+ pozisyon ipucu).
/// </summary>
public static class NewsImportanceAnalyzer
{
    private static readonly (string Type, string[] Keywords)[] TypeRules =
    {
        ("Sakatlık",        new[]{ "sakat", "injur", "adale", "yaralan", "ameliyat", "hamstring", "kas " }),
        ("Ceza",            new[]{ "ceza", "kart gör", "kırmızı kart", "men ", "suspend", "diskalifiye", "cezalı" }),
        ("İlk 11",          new[]{ "ilk 11", "ilk on bir", "muhtemel 11", "kadro açıkland", "lineup", "starting" }),
        ("Teknik Direktör", new[]{ "teknik direktör", "hoca", "rotasyon", "sürpriz karar", "coach", "manager" }),
        ("Transfer",        new[]{ "transfer", "imza", "bonservis", "anlaşma sağla", "kadroya kat" }),
        ("Antrenman",       new[]{ "antrenman", "idman", "hazırlık", "training", "çalışma tamam" }),
        ("Kulüp Açıklaması",new[]{ "kulüp açıkla", "resmi açıkla", "resmi duyuru", "federasyon", "official statement" }),
        ("Basın Açıklaması",new[]{ "basın toplant", "açıklama yaptı", "konuştu", "röportaj", "press" }),
    };

    private static readonly Dictionary<string, int> TypeBase = new()
    {
        ["Sakatlık"] = 34, ["Ceza"] = 30, ["İlk 11"] = 32, ["Teknik Direktör"] = 24,
        ["Transfer"] = 18, ["Kulüp Açıklaması"] = 16, ["Antrenman"] = 12, ["Basın Açıklaması"] = 10,
    };

    public static string Classify(string headline, string summary, string sourceType)
    {
        var text = ((headline ?? "") + " " + (summary ?? "")).ToLowerInvariant();
        foreach (var rule in TypeRules)
            if (rule.Keywords.Any(k => text.Contains(k)))
                return rule.Type;

        // Kategori yoksa SourceType ipucu:
        return sourceType switch
        {
            "Official" => "Kulüp Açıklaması",
            "Roportaj" => "Basın Açıklaması",
            _          => "Basın Açıklaması",
        };
    }

    public static int Score(string type, string headline, DateTime publishedAtUtc, bool authorVerified, string source, DateTime nowUtc)
    {
        var score = TypeBase.GetValueOrDefault(type, 10);

        // Güncellik
        var ageHours = (nowUtc - publishedAtUtc).TotalHours;
        score += ageHours <= 3 ? 24 : ageHours <= 12 ? 16 : ageHours <= 24 ? 10 : ageHours <= 48 ? 4 : 0;

        // Kaynak güvenilirliği
        if (authorVerified) score += 10;
        var src = (source ?? "").ToLowerInvariant();
        if (src.Contains("resmi") || src.Contains("kulüp") || src.Contains("official")) score += 8;

        // Kritik pozisyon ipucu (oyuncu önemi yaklaşık)
        var h = (headline ?? "").ToLowerInvariant();
        if (h.Contains("orta saha") || h.Contains("stoper") || h.Contains("kaleci") || h.Contains("forvet"))
            score += 5;

        return Math.Clamp(score, 0, 100);
    }

    public static string Label(int score)
        => score >= 60 ? "critical" : score >= 42 ? "high" : score >= 24 ? "medium" : "low";
}
