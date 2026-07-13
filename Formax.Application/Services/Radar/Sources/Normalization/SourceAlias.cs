namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — one alias mapping: a raw name variant and the
    /// canonical entity it resolves to. Curated data (Layer-1 exact resolution).
    ///
    /// This sprint sources aliases from an in-memory provider; a durable alias table
    /// can back the same model later without changing the resolver.
    /// </summary>
    public sealed class SourceAlias
    {
        /// <summary>Raw variant as seen in feeds, e.g. "Man Utd", "Manchester Utd".</summary>
        public string Alias { get; init; } = string.Empty;

        /// <summary>Canonical name, e.g. "Manchester United".</summary>
        public string CanonicalName { get; init; } = string.Empty;

        /// <summary>Resolved FORMAX team id when the alias is a team; null otherwise.</summary>
        public int? CanonicalTeamId { get; init; }

        /// <summary>Entity kind, e.g. "team".</summary>
        public string EntityType { get; init; } = "team";
    }
}
