using System.Collections.Generic;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.DTOs.Home
{
    public sealed class HomeRadarMatchDto
    {
        public int MatchId { get; init; }
        public HomeTeamsDto Teams { get; init; } = new();
        public string League { get; init; } = string.Empty;
        public int Sapma { get; init; }
        public int RadarScore { get; init; }
        public int TeamInterestScore { get; init; }
        public int LeagueInterestScore { get; init; }
        public int ContentInterestScore { get; init; }
        public int BehaviorMomentumScore { get; init; }
        public int MatchHeatScore { get; init; }
        public int LeagueBaselineScore { get; init; }
        public int TimeProximityScore { get; init; }
        public string Freshness { get; init; } = string.Empty;
        public bool SessizMi { get; init; }
        public string Explainability { get; init; } = string.Empty;
        public IReadOnlyList<string> Reasons { get; init; } = new List<string>();
        public string LeagueName { get; set; } = "";

        public double PlayRate { get; set; }
        public double TrendDelta { get; set; }

        // 🔥 CRITICAL (DECAY ENGINE İÇİN)
        public TrendDto? Trend { get; set; }
        public double OddsMovementScore { get; set; }
        public int ViewDurationMs { get; set; }
        public double UserSwipeScore { get; set; }
        public bool OpenedDetail { get; set; }
        public bool Followed { get; set; }
    }
}