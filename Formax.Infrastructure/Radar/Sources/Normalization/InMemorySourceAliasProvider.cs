using System.Collections.Generic;
using Formax.Application.Services.Radar.Sources.Normalization;

namespace Formax.Infrastructure.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — in-memory Layer-1 alias dataset. Seeded with the
    /// FORMAX teams so entity resolution is demonstrable without a DB (no migration,
    /// staging forbidden this sprint). Replaced by a durable alias table later behind
    /// the same <see cref="ISourceAliasProvider"/> contract.
    /// </summary>
    public sealed class InMemorySourceAliasProvider : ISourceAliasProvider
    {
        private static readonly IReadOnlyList<SourceAlias> Aliases = new List<SourceAlias>
        {
            new() { Alias = "Galatasaray",  CanonicalName = "Galatasaray",  CanonicalTeamId = 1, EntityType = "team" },
            new() { Alias = "GS",           CanonicalName = "Galatasaray",  CanonicalTeamId = 1, EntityType = "team" },
            new() { Alias = "Galatasaray SK", CanonicalName = "Galatasaray", CanonicalTeamId = 1, EntityType = "team" },

            new() { Alias = "Fenerbahçe",   CanonicalName = "Fenerbahçe",   CanonicalTeamId = 2, EntityType = "team" },
            new() { Alias = "Fenerbahce",   CanonicalName = "Fenerbahçe",   CanonicalTeamId = 2, EntityType = "team" },
            new() { Alias = "FB",           CanonicalName = "Fenerbahçe",   CanonicalTeamId = 2, EntityType = "team" },

            new() { Alias = "Beşiktaş",     CanonicalName = "Beşiktaş",     CanonicalTeamId = 3, EntityType = "team" },
            new() { Alias = "Besiktas",     CanonicalName = "Beşiktaş",     CanonicalTeamId = 3, EntityType = "team" },
            new() { Alias = "BJK",          CanonicalName = "Beşiktaş",     CanonicalTeamId = 3, EntityType = "team" },

            new() { Alias = "Trabzonspor",  CanonicalName = "Trabzonspor",  CanonicalTeamId = 4, EntityType = "team" },
            new() { Alias = "TS",           CanonicalName = "Trabzonspor",  CanonicalTeamId = 4, EntityType = "team" },
        };

        public IReadOnlyList<SourceAlias> GetAliases() => Aliases;
    }
}
