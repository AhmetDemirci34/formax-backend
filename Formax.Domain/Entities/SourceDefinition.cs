using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — declarative definition of a single data source.
    ///
    /// Definitions are authored in appsettings (Radar:Sources[]) and synced into
    /// this table on startup by the registry bootstrapper, so they survive restarts
    /// and can be inspected/extended at runtime. Mutable runtime metrics live on the
    /// paired <see cref="SourceStatus"/> row — definition (config) and status (state)
    /// are intentionally separated.
    ///
    /// This sprint persists the shape only. Collectors, scheduler and failover
    /// execution are implemented in later R.8.x sprints.
    /// </summary>
    public sealed class SourceDefinition
    {
        public int Id { get; set; }

        /// <summary>Stable unique key (e.g. "bridge_thesportsdb", "rss_nabiz",
        /// "internal_useractions"). Survives endpoint/URL changes.</summary>
        public string SourceKey { get; set; } = string.Empty;

        /// <summary>Human-readable label.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Collection mechanism (Internal / Rss / Bridge / Html).</summary>
        public SourceType Type { get; set; }

        /// <summary>Kind of data provided (Match / News / Behavior / Odds).</summary>
        public SourceCategory Category { get; set; }

        /// <summary>Failover group key. Sources in the same group are mutual fallbacks,
        /// selected by ascending <see cref="Priority"/>. Example: "match_fixtures".</summary>
        public string FailoverGroup { get; set; } = string.Empty;

        /// <summary>Selection order within the failover group (lower = preferred).</summary>
        public int Priority { get; set; }

        /// <summary>Fetch endpoint / URL. Null for Internal sources.</summary>
        public string? Endpoint { get; set; }

        /// <summary>Schedule expression (cron or interval token). Interpreted by the
        /// scheduler in a later sprint; stored as opaque text here.</summary>
        public string? ScheduleExpr { get; set; }

        /// <summary>Operator permission to use this source. Distinct from the runtime
        /// <see cref="SourceStatus.Active"/> flag — the engine never flips Enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Permanent vs Temporary bridge (Replace-Then-Remove tracking).</summary>
        public SourceLifecycle Lifecycle { get; set; } = SourceLifecycle.Permanent;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
