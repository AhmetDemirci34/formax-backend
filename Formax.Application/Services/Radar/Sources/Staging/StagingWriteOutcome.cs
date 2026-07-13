namespace Formax.Application.Services.Radar.Sources.Staging
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — outcome of a staging write attempt.
    /// </summary>
    public enum StagingWriteOutcome
    {
        /// <summary>One or more canonical items were persisted.</summary>
        Written = 0,

        /// <summary>Nothing to write (no canonical items).</summary>
        Empty = 1,

        /// <summary>Persistence failed.</summary>
        Failed = 2
    }
}
