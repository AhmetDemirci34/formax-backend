using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.3) — normalized news impact value and its band.
    /// </summary>
    public sealed class NewsImpactScore
    {
        /// <summary>0..100.</summary>
        public double Value { get; init; }

        public NewsImpactLevel Level { get; init; }

        public static NewsImpactScore Of(double value, NewsImpactLevel level)
            => new() { Value = value, Level = level };
    }
}
