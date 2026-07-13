using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Infrastructure.Radar.Sources
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — appsettings binding for source definitions.
    /// Bound from configuration section "Radar". Mirrors the NABIZ options pattern.
    ///
    /// Definitions authored here are synced into the SourceDefinitions table by
    /// <see cref="RadarSourceRegistryBootstrapper"/> on startup, so config stays in
    /// version control while runtime status lives in the database.
    /// </summary>
    public sealed class RadarSourceOptions
    {
        public List<RadarSourceConfig> Sources { get; set; } = new();
    }

    /// <summary>Declarative definition of a single source (one entry under Radar:Sources).</summary>
    public sealed class RadarSourceConfig
    {
        public string SourceKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public SourceType Type { get; set; }
        public SourceCategory Category { get; set; }

        public string FailoverGroup { get; set; } = string.Empty;
        public int Priority { get; set; }

        public string? Endpoint { get; set; }
        public string? ScheduleExpr { get; set; }

        public bool Enabled { get; set; } = true;
        public SourceLifecycle Lifecycle { get; set; } = SourceLifecycle.Permanent;
    }
}
