using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict.Resolvers;

/// <summary>
/// Son güncellenme kriteri. En güncel <see cref="ConflictCandidate{T}.LastUpdatedUtc"/>'ye sahip TEK adayı
/// seçer. Zaman bilgisi hiç yoksa ya da beraberlik varsa karar vermez.
/// </summary>
public sealed class LastUpdatedConflictResolver<T> : IConflictResolver<T>
{
    public string Name => "LastUpdated";

    public ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates)
    {
        var dated = candidates?
            .Where(c => c is not null && c.LastUpdatedUtc.HasValue)
            .ToList();

        if (dated is null || dated.Count == 0)
            return null;

        var latest = dated.Max(c => c.LastUpdatedUtc!.Value);
        var top = dated.Where(c => c.LastUpdatedUtc!.Value == latest).ToList();
        if (top.Count != 1)
            return null;

        return new ConflictResolverDecision<T> { Selected = top[0], Confidence = 1.0d };
    }
}
