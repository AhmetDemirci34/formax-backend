namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.4) — origin of an enrichment item read from staging.
    /// Only these three are consumed this sprint; Odds / Commentary / Feed are out of scope.
    /// </summary>
    public enum ContextEnrichmentSource
    {
        /// <summary>News-category staged item.</summary>
        News = 0,

        /// <summary>Behaviour / internal-signal staged item.</summary>
        InternalSignal = 1,

        /// <summary>Source metadata (key, category, freshness) carried for traceability.</summary>
        SourceMetadata = 2
    }
}
