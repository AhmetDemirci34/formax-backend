namespace Formax.Infrastructure.Radar.Sources.Scheduling
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — scheduler configuration, bound from
    /// "Radar:Scheduler" in appsettings. All values have safe code defaults so the
    /// scheduler runs even with an empty config section.
    /// </summary>
    public sealed class SchedulerOptions
    {
        /// <summary>Seconds between scheduler cycles.</summary>
        public int LoopDelaySeconds { get; set; } = 30;

        /// <summary>Startup delay before the first cycle (lets the app warm up).</summary>
        public int StartupDelaySeconds { get; set; } = 5;

        /// <summary>Max jitter (seconds) subtracted from a source's interval, derived
        /// deterministically per source so a category doesn't fire all at once.</summary>
        public int JitterSeconds { get; set; } = 10;

        // ── Category intervals (seconds) ──────────────────────────────────────
        public int MatchIntervalSeconds { get; set; } = 21600;   // 6h
        public int NewsIntervalSeconds { get; set; } = 900;      // 15m
        public int BehaviorIntervalSeconds { get; set; } = 60;   // 60s
        public int OddsIntervalSeconds { get; set; } = 60;       // 60s
    }
}
