using System.Collections.Generic;

namespace Formax.Application.Coverage
{
    /// <summary>
    /// Timeline Coverage Dashboard — GDP takım Timeline'ının operasyonel görünümü.
    /// Tüm değerler GERÇEK veriden (Teams + Matches) hesaplanır; tahmin yok.
    /// </summary>
    public sealed record TimelineDashboard
    {
        public int TotalTeams { get; init; }
        public int TimelineBuiltTeams { get; init; }       // senkronlanmış VE en az 1 maçı olan
        public int TimelineSyncedTeams { get; init; }      // TimelineSyncedAt != null (denenmiş)
        public int ColdStartPending { get; init; }         // TimelineSyncedAt == null
        public int IncrementalTracked { get; init; }       // senkronlanmış → refresh rotasyonunda
        public int FailedTimelineTeams { get; init; }      // denenmiş ama 0 veri (kapsam yok)

        public double AvgPastMatchesPerTeam { get; init; }        // tüm takımlar
        public double AvgFutureMatchesPerTeam { get; init; }
        public double AvgPastMatchesPerBuiltTeam { get; init; }   // yalnız timeline'ı kurulanlar
        public double AvgFutureMatchesPerBuiltTeam { get; init; }
        public int MaxPastMatches { get; init; }
        public int MaxFutureMatches { get; init; }

        public int TimelineCoveragePercent { get; init; }   // built / total
        public int HistoricalCoveragePercent { get; init; } // >= HistoricalTargetMatches geçmiş / total
        public int FutureCoveragePercent { get; init; }     // >= 1 gelecek / total

        public int HistoricalTargetMatches { get; init; }   // eşik (config, vars. 20 hedef→10 anlamlı)

        public string? LastSyncAtUtc { get; init; }         // MAX(TimelineSyncedAt)
        public string? LastCycleAtUtc { get; init; }        // telemetri: son cycle
        public long LastCycleDurationMs { get; init; }
        public int LastCycleTeams { get; init; }
        public int LastCycleMatchesAdded { get; init; }
        public long ApiFailedRequests { get; init; }        // metrics: başarısız API isteği

        public string ComputedAtUtc { get; init; } = "";
        public bool FromCache { get; init; }
    }

    /// <summary>Lig bazında Timeline raporu satırı.</summary>
    public sealed class LeagueTimelineCoverage
    {
        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = "";
        public int TeamCount { get; init; }
        public int TimelineBuiltTeams { get; init; }
        public int ColdStartPending { get; init; }
        public double AvgPastMatches { get; init; }
        public double AvgFutureMatches { get; init; }
        public int CoveragePercent { get; init; }
        public int HistoricalPercent { get; init; }
        public int FuturePercent { get; init; }
        public string? LastUpdateUtc { get; init; }
        public string DataQuality { get; init; } = "";   // Ready | Partial | Cold | Empty
        public int MatchCount { get; init; }
    }

    /// <summary>API Quota Intelligence — gerçek ölçüm + türetilmiş kapasite.</summary>
    public sealed class TimelineQuotaReport
    {
        // Gerçek ölçüm (ApiFootballMetrics — process ömrü)
        public long TotalRequests { get; init; }
        public long FailedRequests { get; init; }
        public long CacheHits { get; init; }
        public int CacheGainPercent { get; init; }
        public double RequestsPerHour { get; init; }
        public double RequestsPerDayProjected { get; init; }
        public Dictionary<string, long> RequestsByEndpoint { get; init; } = new();

        // Job attribution — GERÇEK HTTP denemesi başına (retry dahil, cache-hit hariç).
        // RequestsByEndpoint'ten bağımsızdır; o sayacın anlamı değişmemiştir.
        public long JobAttributedRequests { get; init; }
        public Dictionary<string, long> RequestsByJob { get; init; } = new();
        /// <summary>Anahtar biçimi: "{job}|{endpointFamily}".</summary>
        public Dictionary<string, long> RequestsByJobAndEndpoint { get; init; } = new();

        // Timeline maliyet modeli (sabit, koddan: last + next = 2 istek/takım)
        public int RequestsPerTeamTimeline { get; init; }
        public int ColdStartCostPerTeam { get; init; }
        public int IncrementalCostPerTeamRefresh { get; init; }

        // Kapsam durumu (DB)
        public int ColdStartPendingTeams { get; init; }
        public int ColdStartRemainingRequests { get; init; }   // pending × 2

        // Plan limiti (api-football /status — gerçek; alınamazsa config fallback)
        public int DailyRequestLimit { get; init; }
        public int DailyRequestsUsedToday { get; init; }       // /status current
        public string DailyLimitSource { get; init; } = "";    // "api-football/status" | "config" | "unknown"
        public int RefreshIntervalHours { get; init; }

        // Türetilmiş kapasite (gerçek maliyet + gerçek limit)
        public int SafeDailyBudget { get; init; }              // limit × safety
        public int ManageableTeams { get; init; }              // "kaç takım rahatlıkla yönetilir"
        public string CapacityVerdict { get; init; } = "";

        public string ComputedAtUtc { get; init; } = "";
    }

    /// <summary>Cold Start önceliklendirme önizlemesi — bir sonraki batch'te seçilecek takımlar (API çağrısı YOK).</summary>
    public sealed class ColdStartPreview
    {
        public int MaxTeamsPerCycle { get; init; }
        public int RefreshIntervalHours { get; init; }
        public int EligibleTeams { get; init; }
        public Dictionary<string, int> TierBreakdown { get; init; } = new();
        public List<ColdStartCandidate> NextBatch { get; init; } = new();
        public string ComputedAtUtc { get; init; } = "";
    }

    public sealed class ColdStartCandidate
    {
        public int TeamId { get; init; }
        public string? ExternalTeamId { get; init; }
        public string TeamName { get; init; } = "";
        public int PriorityTier { get; init; }
        public string PriorityReason { get; init; } = "";
        public string? NearestMatchUtc { get; init; }
        public string? TimelineSyncedAtUtc { get; init; }
    }
}
