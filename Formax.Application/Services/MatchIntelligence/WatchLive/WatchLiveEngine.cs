using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.WatchLive;

public interface IWatchLiveEngine
{
    WatchLiveDto Analyze(string? league, string status, DateTime matchUtc, string homeTeam, string awayTeam);
}

/// <summary>
/// Watch Live Engine (Görev #018). Lig → resmi/lisanslı yayıncı eşlemesi + maç durumundan availability
/// → en uygun resmi seçenek + AI önerisi + evidence. Yasa dışı kaynak KULLANMAZ; oran/bahis içermez.
/// Bilinmeyen lig için uydurma yapmaz (generic "resmi yayıncı").
/// </summary>
public sealed class WatchLiveEngine : IWatchLiveEngine
{
    public WatchLiveDto Analyze(string? league, string status, DateTime matchUtc, string homeTeam, string awayTeam)
    {
        var now = DateTime.UtcNow;

        var b = BroadcasterAnalyzer.Resolve(league);
        var (availability, label) = AvailabilityAnalyzer.Analyze(status, matchUtc, now);
        var recommendation = RecommendationEngine.Recommend(b, availability);

        var confidence = b.IsSpecific
            ? (availability == "unavailable" ? 60 : 88)
            : (availability == "unavailable" ? 40 : 55);

        return new WatchLiveDto
        {
            Broadcaster = b.Broadcaster,
            Platform = b.Platform,
            Availability = availability,
            AvailabilityLabel = label,
            Region = b.Region,
            StreamQuality = b.Quality,
            MatchCoverage = b.Coverage,
            AiRecommendation = recommendation,
            HeroSummary = WatchLiveSummaryGenerator.Hero(b, label),
            AiSummary = WatchLiveSummaryGenerator.Summary(b, availability, label),
            IsSpecificBroadcaster = b.IsSpecific,
            Confidence = confidence,
            Evidence = WatchLiveEvidenceBuilder.Build(b, label),
        };
    }
}
