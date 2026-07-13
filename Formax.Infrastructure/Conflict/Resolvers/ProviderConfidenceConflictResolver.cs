using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict.Resolvers;

/// <summary>
/// Provider güveni kriteri. En yüksek <see cref="ConflictCandidate{T}.ProviderConfidence"/>'a sahip adayı
/// seçer; confidence olarak da o değeri döndürür. Güven bilgisi hiç yoksa karar vermez.
/// </summary>
public sealed class ProviderConfidenceConflictResolver<T> : IConflictResolver<T>
{
    public string Name => "ProviderConfidence";

    public ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates)
    {
        var withConfidence = candidates?
            .Where(c => c is not null && c.ProviderConfidence.HasValue)
            .ToList();

        if (withConfidence is null || withConfidence.Count == 0)
            return null;

        var best = withConfidence.OrderByDescending(c => c.ProviderConfidence!.Value).First();
        return new ConflictResolverDecision<T> { Selected = best, Confidence = best.ProviderConfidence!.Value };
    }
}
