using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict.Resolvers;

/// <summary>
/// Provider önceliği kriteri. En yüksek önceliğe sahip TEK aday varsa onu seçer.
/// Öncelik bilgisi yoksa (0) ya da beraberlik varsa karar vermez. Öncelik değerleri Configuration'dan
/// gelir (adaya dışarıdan yerleştirilir); strateji hiçbir hardcoded öncelik içermez.
/// </summary>
public sealed class ProviderPriorityConflictResolver<T> : IConflictResolver<T>
{
    public string Name => "ProviderPriority";

    public ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return null;

        var maxPriority = candidates.Max(c => c.ProviderPriority);
        if (maxPriority <= 0)
            return null;

        var top = candidates.Where(c => c.ProviderPriority == maxPriority).ToList();
        if (top.Count != 1)
            return null;

        return new ConflictResolverDecision<T> { Selected = top[0], Confidence = 1.0d };
    }
}
