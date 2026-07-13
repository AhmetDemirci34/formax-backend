namespace Formax.Application.Services.Radar.Sources.Monitor
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — outcome of one monitor evaluation.
    /// </summary>
    public enum MonitorEvaluationOutcome
    {
        /// <summary>First evaluation for the source — snapshot created.</summary>
        Created = 0,

        /// <summary>Existing snapshot updated.</summary>
        Updated = 1,

        /// <summary>No health data yet — nothing evaluated.</summary>
        NoHealthData = 2,

        /// <summary>Evaluation could not be persisted.</summary>
        Failed = 3
    }
}
