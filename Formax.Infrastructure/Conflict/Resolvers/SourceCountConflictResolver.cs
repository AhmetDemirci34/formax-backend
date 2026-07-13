using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;

namespace Formax.Infrastructure.Conflict.Resolvers;

/// <summary>
/// Kaynak sayısı kriteri. Aynı değeri destekleyen kaynak sayılarını toplar; en çok desteklenen TEK
/// değeri seçer. Confidence = kazanan pay / toplam. Beraberlikte karar vermez.
/// </summary>
public sealed class SourceCountConflictResolver<T> : IConflictResolver<T>
{
    public string Name => "SourceCount";

    public ConflictResolverDecision<T>? Resolve(IReadOnlyList<ConflictCandidate<T>> candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return null;

        var comparer = EqualityComparer<T>.Default;
        var groups = new List<(ConflictCandidate<T> Representative, int Count)>();

        foreach (var candidate in candidates)
        {
            if (candidate is null)
                continue;

            var index = groups.FindIndex(g => comparer.Equals(g.Representative.Value, candidate.Value));
            if (index >= 0)
                groups[index] = (groups[index].Representative, groups[index].Count + candidate.SourceCount);
            else
                groups.Add((candidate, candidate.SourceCount));
        }

        if (groups.Count == 0)
            return null;

        var total = groups.Sum(g => g.Count);
        var maxCount = groups.Max(g => g.Count);
        var top = groups.Where(g => g.Count == maxCount).ToList();
        if (top.Count != 1)
            return null;

        var confidence = total > 0 ? (double)maxCount / total : 0d;
        return new ConflictResolverDecision<T> { Selected = top[0].Representative, Confidence = confidence };
    }
}
