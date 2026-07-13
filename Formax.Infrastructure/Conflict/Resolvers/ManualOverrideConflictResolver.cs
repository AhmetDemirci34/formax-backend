using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict.Resolvers;

/// <summary>
/// Manuel geçersiz kılma kriteri. Manuel override işaretli bir aday varsa onu tam confidence ile seçer.
/// </summary>
public sealed class ManualOverrideConflictResolver<T> : IConflictResolver<T>
{
    public string Name => "ManualOverride";

    public ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates)
    {
        var overridden = candidates?.FirstOrDefault(c => c is not null && c.IsManualOverride);
        return overridden is null
            ? null
            : new ConflictResolverDecision<T> { Selected = overridden, Confidence = 1.0d };
    }
}
