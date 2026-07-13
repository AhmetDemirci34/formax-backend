using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Market;

/// <summary>
/// Market eğilimi: OynanmaYonu + güncel OynanmaSkoru'ndan yön/eğilim (bahis/oran DEĞİL) (Görev #017).
/// </summary>
public static class MarketTrendAnalyzer
{
    /// <param name="current">Güncel OynanmaSkoru (0–100, 50=denge).</param>
    public static (string Trend, string Direction, int Lean) Analyze(string oynanmaYonu, int current)
    {
        var mag = Math.Clamp(Math.Abs(current - 50), 0, 50);
        var yon = (oynanmaYonu ?? "").ToLowerInvariant();
        if (yon.Contains("home")) return ("Ev sahibi yönünde", "home", Math.Clamp(50 - mag, 0, 100));
        if (yon.Contains("away")) return ("Deplasman yönünde", "away", Math.Clamp(50 + mag, 0, 100));
        return ("Dengeli", "balanced", 50);
    }

    public static string StateLabel(int score, string direction)
    {
        if (Math.Abs(score - 50) < 3) return "Dengeli";
        return direction == "home" ? "Ev sahibi ağırlıklı"
             : direction == "away" ? "Deplasman ağırlıklı"
             : "Hareketli";
    }
}

/// <summary>Volatilite: ardışık oynanma değişimlerinin ortalama büyüklüğü.</summary>
public static class MarketVolatilityAnalyzer
{
    public static int Volatility(IReadOnlyList<int> series)
    {
        if (series.Count < 2) return 0;
        double sum = 0;
        for (var i = 1; i < series.Count; i++) sum += Math.Abs(series[i] - series[i - 1]);
        var avgChange = sum / (series.Count - 1);
        return Math.Clamp((int)Math.Round(avgChange * 12), 0, 100);
    }

    public static string Level(int v) => v >= 60 ? "Yüksek" : v >= 30 ? "Orta" : "Düşük";
}

/// <summary>Kararlılık: düşük volatilite + tutarlı yön → yüksek kararlılık.</summary>
public static class MarketStabilityAnalyzer
{
    public static int Stability(IReadOnlyList<int> series, int volatility)
    {
        if (series.Count < 2) return Math.Clamp(70 - volatility, 0, 100); // tek nokta: hareket gözlenmedi

        var signChanges = 0;
        var prevSign = 0;
        for (var i = 1; i < series.Count; i++)
        {
            var s = Math.Sign(series[i] - series[i - 1]);
            if (s != 0)
            {
                if (prevSign != 0 && s != prevSign) signChanges++;
                prevSign = s;
            }
        }
        return Math.Clamp(90 - volatility - signChanges * 5, 0, 100);
    }

    public static string Level(int s) => s >= 67 ? "Yüksek" : s >= 40 ? "Orta" : "Düşük";
}

/// <summary>Sapma: piyasa oynanması ile gerçek güç arasındaki ölçüm farkı (Sapma motorundan).</summary>
public static class MarketDeviationAnalyzer
{
    public static MarketDeviationDto Analyze(int sapma, string sapmaBolgesi, string sapmaMetni)
        => new()
        {
            Score = sapma,
            Region = sapmaBolgesi ?? "",
            Note = string.IsNullOrWhiteSpace(sapmaMetni) ? "" : sapmaMetni,
        };

    public static string Direction(string gercekGucYonu)
    {
        var y = (gercekGucYonu ?? "").ToLowerInvariant();
        return y.Contains("home") ? "home" : y.Contains("away") ? "away" : "balanced";
    }
}
