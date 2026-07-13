using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Market;

/// <summary>Hero Market Summary + AI Summary üretir (Görev #017). Oran/bahis içermez.</summary>
public static class MarketSummaryGenerator
{
    public static string Hero(string trend, int stability, int volatility)
    {
        var stabWord = MarketStabilityAnalyzer.Level(stability).ToLowerInvariant();
        var volWord = MarketVolatilityAnalyzer.Level(volatility).ToLowerInvariant();

        return trend == "Dengeli"
            ? $"Market açılıştan bu yana büyük ölçüde dengede kaldı; hareket {stabWord} kararlılıkta ve {volWord} oynaklıkta."
            : $"Market açılıştan bu yana {trend.ToLowerInvariant()} hareket etti; seyir {stabWord} kararlılıkta ve {volWord} oynaklıkta.";
    }

    public static string Summary(
        string trend, string openingState, string currentState,
        MarketDeviationDto dev, int stability, int volatility)
    {
        var s = $"Market {trend.ToLowerInvariant()}: açılışta {openingState.ToLowerInvariant()}, güncelde {currentState.ToLowerInvariant()}.";
        s += $" Hareket {MarketStabilityAnalyzer.Level(stability).ToLowerInvariant()} kararlılıkta, " +
             $"{MarketVolatilityAnalyzer.Level(volatility).ToLowerInvariant()} oynaklıkta.";
        if (!string.IsNullOrWhiteSpace(dev.Region))
            s += $" Ölçüm farkı: {dev.Region.ToLowerInvariant()}.";
        return s;
    }
}

/// <summary>Evidence: Eğilim, Sapma, Kararlılık, Volatilite, Açılış → Güncel (Görev #017).</summary>
public static class MarketEvidenceBuilder
{
    public static List<MarketEvidenceItemDto> Build(
        string trend, MarketDeviationDto dev, int stability, int volatility,
        string openingState, string currentState)
        => new()
        {
            new() { Label = "Eğilim", Detail = trend },
            new()
            {
                Label = "Sapma",
                Detail = dev.Score > 0
                    ? (string.IsNullOrEmpty(dev.Region) ? dev.Score.ToString() : $"{dev.Region} ({dev.Score})")
                    : (string.IsNullOrEmpty(dev.Region) ? "—" : dev.Region)
            },
            new() { Label = "Kararlılık", Detail = MarketStabilityAnalyzer.Level(stability) },
            new() { Label = "Volatilite", Detail = MarketVolatilityAnalyzer.Level(volatility) },
            new() { Label = "Açılış → Güncel", Detail = $"{openingState} → {currentState}" },
        };
}
