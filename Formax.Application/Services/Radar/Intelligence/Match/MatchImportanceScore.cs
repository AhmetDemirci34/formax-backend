using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.6) — the normalized importance value and its band.
    /// </summary>
    public sealed class MatchImportanceScore
    {
        /// <summary>0..100.</summary>
        public double Value { get; init; }

        public MatchImportanceLevel Level { get; init; }

        public static MatchImportanceScore Of(double value, MatchImportanceLevel level)
            => new() { Value = value, Level = level };
    }
}
