using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Health
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — result of recording an execution outcome for a
    /// source: the recomputed score/status plus what happened to the snapshot.
    /// </summary>
    public sealed class HealthCalculationResult
    {
        public int SourceId { get; init; }
        public string SourceKey { get; init; } = string.Empty;

        public double HealthScore { get; init; }
        public SourceHealthStatus Status { get; init; }

        public HealthCalculationOutcome Outcome { get; init; }
        public string? Error { get; init; }

        public static HealthCalculationResult Failed(int sourceId, string sourceKey, string error)
            => new()
            {
                SourceId = sourceId,
                SourceKey = sourceKey,
                Outcome = HealthCalculationOutcome.Failed,
                Error = error,
                Status = SourceHealthStatus.Unknown
            };
    }
}
