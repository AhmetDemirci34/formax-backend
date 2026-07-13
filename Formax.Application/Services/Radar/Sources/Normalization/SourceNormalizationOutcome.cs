namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — per-item outcome of normalization.
    /// </summary>
    public enum SourceNormalizationOutcome
    {
        /// <summary>Structurally valid and entity-resolved → produced a CanonicalSourceItem.</summary>
        Normalized = 0,

        /// <summary>Structurally valid but entity could not be resolved → UnresolvedSourceItem.</summary>
        Unresolved = 1,

        /// <summary>Structurally invalid (e.g. empty raw name) → dropped from canonical output.</summary>
        Invalid = 2
    }
}
