using System.Collections.Generic;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — resolves the collector for a source by its type.
    /// </summary>
    public interface ISourceCollectorFactory
    {
        /// <summary>Collector for the definition's type, or null if none registered.</summary>
        ISourceCollector? Resolve(SourceDefinition definition);

        /// <summary>Collector for a type, or null if none registered.</summary>
        ISourceCollector? ResolveByType(SourceType type);

        /// <summary>All known type→collector registrations (diagnostics).</summary>
        IReadOnlyList<CollectorRegistration> Registrations { get; }
    }
}
