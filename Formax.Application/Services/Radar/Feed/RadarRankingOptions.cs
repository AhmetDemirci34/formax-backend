namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.5) — config for the Radar ranking support signal. Bound from
    /// "RadarRanking" in appsettings. Influence is capped so Radar can only ever be a
    /// low-weight support layer — never a ranking replacement.
    /// </summary>
    public sealed class RadarRankingOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Radar weight in the blended sort key (0..1). Hard-capped at 0.15.</summary>
        public double Influence { get; set; } = 0.15;
    }
}
