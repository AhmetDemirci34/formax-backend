namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — resolves a raw name to a canonical entity using
    /// the alias dataset. Layer-1 strategy only: exact match + normalized (case/accent/
    /// suffix-insensitive) match. Fuzzy matching is deferred to a later sprint.
    /// </summary>
    public interface ISourceAliasResolver
    {
        AliasResolution Resolve(string rawName);
    }

    /// <summary>Result of an alias lookup.</summary>
    public sealed class AliasResolution
    {
        public bool Resolved { get; init; }
        public string CanonicalName { get; init; } = string.Empty;
        public int? CanonicalTeamId { get; init; }
        public string EntityType { get; init; } = string.Empty;

        public static AliasResolution Miss() => new() { Resolved = false };

        public static AliasResolution Hit(SourceAlias alias) => new()
        {
            Resolved = true,
            CanonicalName = alias.CanonicalName,
            CanonicalTeamId = alias.CanonicalTeamId,
            EntityType = alias.EntityType
        };
    }
}
