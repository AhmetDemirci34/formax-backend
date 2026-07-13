using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — supplies the alias dataset used for entity
    /// resolution. In-memory in this sprint; a durable table can implement the same
    /// contract later without touching the resolver.
    /// </summary>
    public interface ISourceAliasProvider
    {
        IReadOnlyList<SourceAlias> GetAliases();
    }
}
