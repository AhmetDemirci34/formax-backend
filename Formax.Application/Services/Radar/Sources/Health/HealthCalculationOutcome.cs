namespace Formax.Application.Services.Radar.Sources.Health
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — outcome of recording one execution into health.
    /// </summary>
    public enum HealthCalculationOutcome
    {
        /// <summary>First execution for the source — snapshot created.</summary>
        Created = 0,

        /// <summary>Existing snapshot updated.</summary>
        Updated = 1,

        /// <summary>Could not record (persistence error).</summary>
        Failed = 2
    }
}
