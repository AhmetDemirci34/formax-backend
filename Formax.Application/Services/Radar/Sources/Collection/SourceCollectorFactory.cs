using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — default factory. Indexes the injected collectors
    /// by their <see cref="ISourceCollector.SupportedType"/>. If two collectors claim
    /// the same type, the first wins (registration order).
    /// </summary>
    public sealed class SourceCollectorFactory : ISourceCollectorFactory
    {
        private readonly Dictionary<SourceType, ISourceCollector> _byType = new();
        private readonly List<CollectorRegistration> _registrations = new();

        public SourceCollectorFactory(IEnumerable<ISourceCollector> collectors)
        {
            foreach (var c in collectors)
            {
                if (_byType.ContainsKey(c.SupportedType))
                    continue;

                _byType[c.SupportedType] = c;
                _registrations.Add(new CollectorRegistration
                {
                    Type = c.SupportedType,
                    Collector = c,
                    CollectorName = c.GetType().Name
                });
            }
        }

        public ISourceCollector? Resolve(SourceDefinition definition)
            => ResolveByType(definition.Type);

        public ISourceCollector? ResolveByType(SourceType type)
            => _byType.TryGetValue(type, out var c) ? c : null;

        public IReadOnlyList<CollectorRegistration> Registrations => _registrations;
    }
}
