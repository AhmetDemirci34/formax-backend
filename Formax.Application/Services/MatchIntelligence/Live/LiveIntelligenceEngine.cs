using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Live;

public interface ILiveIntelligenceEngine
{
    LiveIntelligenceDto Analyze(LiveSectionDto? live, string status, string homeTeam, string awayTeam);
}

/// <summary>
/// Live Intelligence Engine (Görev #019). Canlı maç verilerini (skor/dakika/dangerousAttacks/momentum/
/// olaylar) analiz eder: maç ritmi, momentum, en önemli olay, AI canlı özeti, evidence.
/// Canlı veri (Stats) yoksa DÜRÜST fallback (IsLive=false). Veri uydurulmaz.
/// </summary>
public sealed class LiveIntelligenceEngine : ILiveIntelligenceEngine
{
    public LiveIntelligenceDto Analyze(LiveSectionDto? live, string status, string homeTeam, string awayTeam)
    {
        var (matchStatus, statusLabel) = MapStatus(status);
        var stats = live?.Stats;

        // Canlı veri yok → dürüst fallback (uydurma yok).
        if (stats == null)
        {
            return new LiveIntelligenceDto
            {
                HomeTeam = homeTeam, AwayTeam = awayTeam, IsLive = false,
                MatchStatus = matchStatus, StatusLabel = statusLabel,
                MatchMinute = "", Score = new LiveScoreDto(),
                MatchRhythm = "", Momentum = "balanced", MomentumValue = 50,
                KeyEvent = new LiveKeyEventDto { Type = "—", Description = "Maç öncesi", HasEvent = false },
                Confidence = 0,
                HeroSummary = LiveSummaryGenerator.Fallback(statusLabel),
                AiSummary = LiveSummaryGenerator.Fallback(statusLabel),
                Evidence = LiveEvidenceBuilder.Fallback(),
            };
        }

        var timeline = live!.Timeline ?? new List<LiveEventDto>();
        var momentumSnaps = live.Momentum ?? new List<MomentumSnapshotDto>();

        var minute = stats.Minute != null ? $"{stats.Minute}'" : (matchStatus == "ended" ? "MS" : "");
        var rhythm = LiveRhythmAnalyzer.Rhythm(stats);
        var (momSide, momValue) = LiveMomentumAnalyzer.Momentum(momentumSnaps, stats);
        var keyEvent = LiveEventAnalyzer.KeyEvent(timeline, homeTeam, awayTeam);

        var confidence = Math.Clamp(
            50 + (timeline.Count > 0 ? 15 : 0) + (momentumSnaps.Count > 0 ? 15 : 0) + 10, 0, 90);

        return new LiveIntelligenceDto
        {
            HomeTeam = homeTeam, AwayTeam = awayTeam,
            IsLive = matchStatus is "live" or "halftime",
            MatchMinute = minute,
            Score = new LiveScoreDto { Home = stats.HomeScore, Away = stats.AwayScore },
            MatchStatus = matchStatus, StatusLabel = statusLabel,
            MatchRhythm = rhythm, Momentum = momSide, MomentumValue = momValue, KeyEvent = keyEvent,
            Confidence = confidence,
            HeroSummary = LiveSummaryGenerator.Hero(stats.HomeScore, stats.AwayScore, minute, momSide, homeTeam, awayTeam, keyEvent),
            AiSummary = LiveSummaryGenerator.Summary(stats.HomeScore, stats.AwayScore, minute, rhythm, momSide, homeTeam, awayTeam, keyEvent),
            Evidence = LiveEvidenceBuilder.Build(stats.HomeScore, stats.AwayScore, minute, momSide, keyEvent, rhythm, homeTeam, awayTeam),
        };
    }

    private static (string Status, string Label) MapStatus(string status)
    {
        var s = (status ?? "").ToLowerInvariant();
        if (s.Contains("live")) return ("live", "Canlı");
        if (s.Contains("half") || s == "ht") return ("halftime", "İlk yarı sonu");
        if (s.Contains("finish") || s.Contains("ended") || s == "ft") return ("ended", "Bitti");
        return ("pre", "Maç öncesi");
    }
}
