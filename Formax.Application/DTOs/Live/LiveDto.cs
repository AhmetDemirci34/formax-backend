namespace Formax.Application.DTOs.Live
{
    // ─── Provider result models (returned by ISportsDataProvider) ─────────────

    /// <summary>Full live stat snapshot from the sports data provider.</summary>
    public class SportsLiveStats
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? Minute { get; set; }

        /// <summary>"1H" | "HT" | "2H" | "ET" | "P" | "FT" | ""</summary>
        public string Phase { get; set; } = string.Empty;

        public int PossessionHome { get; set; }
        public int PossessionAway { get; set; }

        public int ShotsHome { get; set; }
        public int ShotsAway { get; set; }
        public int ShotsOnTargetHome { get; set; }
        public int ShotsOnTargetAway { get; set; }

        public int CornersHome { get; set; }
        public int CornersAway { get; set; }
        public int FoulsHome { get; set; }
        public int FoulsAway { get; set; }
        public int OffsidesHome { get; set; }
        public int OffsidesAway { get; set; }
        public int YellowHome { get; set; }
        public int YellowAway { get; set; }
        public int RedHome { get; set; }
        public int RedAway { get; set; }

        public int DangerousAttacksHome { get; set; }
        public int DangerousAttacksAway { get; set; }

        public double? XgHome { get; set; }
        public double? XgAway { get; set; }
    }

    /// <summary>Single event entry from the provider's event timeline.</summary>
    public class SportsLiveEvent
    {
        public int Minute { get; set; }

        /// <summary>Normalised event type: "Goal" | "YellowCard" | "RedCard" | "Substitution" | "Var" | "Other"</summary>
        public string EventType { get; set; } = string.Empty;

        public string TeamName { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string AssistName { get; set; } = string.Empty;

        /// <summary>Raw detail string from the provider, e.g. "Normal Goal", "Second Yellow card".</summary>
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>Derived momentum sample.</summary>
    public class SportsLiveMomentum
    {
        public int HomePressure { get; set; }
        public int AwayPressure { get; set; }
        public int MinuteBucket { get; set; }
    }

    // ─── Batch live endpoint result (one entry per live fixture from provider) ──

    /// <summary>
    /// Lightweight summary returned by the provider's single-call batch live endpoint
    /// (e.g. GET /fixtures?live=all).  Contains only score + clock — no detailed
    /// statistics.  Used by LiveMatchIngestionJob for dirty-checking before issuing
    /// per-match detailed stat requests.
    /// </summary>
    public class SportsLiveBatchEntry
    {
        /// <summary>Provider-side fixture id (e.g. "12345").</summary>
        public string ExternalMatchId { get; set; } = string.Empty;
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? Minute { get; set; }

        /// <summary>"1H" | "HT" | "2H" | "ET" | "P"</summary>
        public string Phase { get; set; } = string.Empty;
    }

    // ─── MatchDetail DTO section models (consumed by the frontend) ────────────

    public class LiveStatsDto
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? Minute { get; set; }
        public string Phase { get; set; } = string.Empty;

        public int PossessionHome { get; set; }
        public int PossessionAway { get; set; }

        public int ShotsHome { get; set; }
        public int ShotsAway { get; set; }
        public int ShotsOnTargetHome { get; set; }
        public int ShotsOnTargetAway { get; set; }

        public int CornersHome { get; set; }
        public int CornersAway { get; set; }
        public int FoulsHome { get; set; }
        public int FoulsAway { get; set; }
        public int OffsidesHome { get; set; }
        public int OffsidesAway { get; set; }
        public int YellowHome { get; set; }
        public int YellowAway { get; set; }
        public int RedHome { get; set; }
        public int RedAway { get; set; }

        public int DangerousAttacksHome { get; set; }
        public int DangerousAttacksAway { get; set; }

        public double? XgHome { get; set; }
        public double? XgAway { get; set; }

        /// <summary>UTC timestamp of the last successful provider fetch.</summary>
        public DateTime UpdatedAt { get; set; }
    }

    public class LiveEventDto
    {
        public int Minute { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public double ImpactScore { get; set; }
    }

    public class MomentumSnapshotDto
    {
        public int Minute { get; set; }
        public int HomePressure { get; set; }
        public int AwayPressure { get; set; }
    }

    public class LiveSectionDto
    {
        /// <summary>Null when match is pre-match and no data exists yet.</summary>
        public LiveStatsDto? Stats { get; set; }

        /// <summary>Chronological event timeline — empty list when no events recorded.</summary>
        public List<LiveEventDto> Timeline { get; set; } = new();

        /// <summary>Momentum series — last 90 samples max, ordered ascending by minute.</summary>
        public List<MomentumSnapshotDto> Momentum { get; set; } = new();
    }
}
