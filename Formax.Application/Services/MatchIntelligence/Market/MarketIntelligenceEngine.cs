using Formax.Application.DTOs.MatchIntelligence;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.MatchIntelligence.Market;

public interface IMarketIntelligenceEngine
{
    MarketIntelligenceDto Analyze(int matchId, Formax.Application.DTOs.Matches.SapmaDto sapma, string homeTeam, string awayTeam);
}

/// <summary>
/// Market Intelligence Engine (Görev #017). "Oynanma yoğunluğu" (OynanmaSkoru 0..100) zaman serisini
/// + Sapma (ölçüm farkı) yorumlar: eğilim, açılış→güncel, volatilite, kararlılık, sapma, AI özeti.
///
/// FORMAX BAHİS DEĞİLDİR: oran/bahis tipi/öneri ÜRETİLMEZ; yalnız market DAVRANIŞI yorumlanır.
/// </summary>
public sealed class MarketIntelligenceEngine : IMarketIntelligenceEngine
{
    private readonly IMatchOynanmaSnapshotReadRepository _snapshots;

    public MarketIntelligenceEngine(IMatchOynanmaSnapshotReadRepository snapshots)
    {
        _snapshots = snapshots;
    }

    public MarketIntelligenceDto Analyze(
        int matchId, Formax.Application.DTOs.Matches.SapmaDto sapma, string homeTeam, string awayTeam)
    {
        // Oynanma yoğunluğu zaman serisi (kronolojik).
        var series = _snapshots.Query()
            .Where(s => s.MatchId == matchId)
            .OrderBy(s => s.CapturedAtUtc)
            .Select(s => s.OynanmaSkoru)
            .ToList();

        var current = series.Count > 0 ? series[^1] : (sapma?.OynanmaSkoru ?? 50);
        var opening = series.Count > 0 ? series[0] : current;

        var (trend, direction, lean) = MarketTrendAnalyzer.Analyze(sapma?.OynanmaYonu ?? "Denge", current);
        var openingState = MarketTrendAnalyzer.StateLabel(opening, direction);
        var currentState = MarketTrendAnalyzer.StateLabel(current, direction);

        var volatility = MarketVolatilityAnalyzer.Volatility(series);
        var stability = MarketStabilityAnalyzer.Stability(series, volatility);
        var deviation = MarketDeviationAnalyzer.Analyze(
            sapma?.Sapma ?? 0, sapma?.SapmaBolgesi ?? "", sapma?.SapmaMetni ?? "");
        var devDirection = MarketDeviationAnalyzer.Direction(sapma?.GercekGucYonu ?? "");

        var confidence = Math.Clamp(
            45 + Math.Min(35, series.Count * 5) - ((sapma?.SessizMi ?? false) ? 10 : 0), 0, 90);

        return new MarketIntelligenceDto
        {
            HomeTeam = homeTeam, AwayTeam = awayTeam,
            MarketTrend = trend, OpeningState = openingState, CurrentState = currentState,
            Direction = direction, Lean = lean, Stability = stability, Volatility = volatility,
            Deviation = deviation,
            SignificantChange = new MarketChangeDto
            {
                Label = string.IsNullOrWhiteSpace(deviation.Region) ? "Market hareketi" : deviation.Region,
                Note = string.IsNullOrWhiteSpace(deviation.Note)
                    ? $"Açılış {openingState.ToLowerInvariant()}, güncel {currentState.ToLowerInvariant()}."
                    : deviation.Note,
                Direction = devDirection,
            },
            ConfidenceSummary = new MarketConfidenceDto { Label = "Güven", Value = confidence },
            Confidence = confidence,
            HeroSummary = MarketSummaryGenerator.Hero(trend, stability, volatility),
            AiSummary = MarketSummaryGenerator.Summary(trend, openingState, currentState, deviation, stability, volatility),
            Evidence = MarketEvidenceBuilder.Build(trend, deviation, stability, volatility, openingState, currentState),
        };
    }
}
