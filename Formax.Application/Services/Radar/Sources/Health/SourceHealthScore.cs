using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Health
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — value object pairing a numeric health score with
    /// its classification. Produced by the pure scoring step.
    /// </summary>
    public sealed class SourceHealthScore
    {
        /// <summary>0..100.</summary>
        public double Score { get; init; }

        public SourceHealthStatus Status { get; init; }

        public static SourceHealthScore Of(double score, SourceHealthStatus status)
            => new() { Score = score, Status = status };
    }
}
