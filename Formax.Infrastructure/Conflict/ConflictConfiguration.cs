using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Merkezi Conflict yapılandırması: strateji sırası, asgari confidence, provider öncelikleri ve
/// strateji ağırlıkları. Bu değerler engine/strategy koduna gömülü DEĞİLDİR; buradan okunur.
/// İleride appsettings / database / admin panelinden beslenebilir.
/// </summary>
public sealed class ConflictConfiguration
{
    /// <summary>Stratejilerin çalışma sırası (Strategy Priority). İlk kesin sonucu veren kazanır.</summary>
    public IReadOnlyList<string> StrategyOrder { get; init; } = DefaultOrder;

    /// <summary>Bir kararın "kesin" sayılması için gereken asgari (ağırlıklı) confidence.</summary>
    public double MinimumConfidence { get; init; } = 0.5d;

    /// <summary>Provider → öncelik (Provider Priority kriteri).</summary>
    public IReadOnlyDictionary<string, int> ProviderPriorities { get; init; } = EmptyPriorities;

    /// <summary>Strateji adı → ağırlık (LastUpdated/SourceCount/ManualOverride vb. ağırlıkları).</summary>
    public IReadOnlyDictionary<string, double> Weights { get; init; } = DefaultWeights;

    private static readonly IReadOnlyList<string> DefaultOrder = new[]
    {
        "ManualOverride",
        "ProviderPriority",
        "ProviderConfidence",
        "SourceCount",
        "LastUpdated"
    };

    private static readonly IReadOnlyDictionary<string, int> EmptyPriorities =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, double> DefaultWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["ManualOverride"] = 1.0d,
            ["ProviderPriority"] = 1.0d,
            ["ProviderConfidence"] = 1.0d,
            ["SourceCount"] = 1.0d,
            ["LastUpdated"] = 1.0d
        };

    public double WeightFor(string strategy) =>
        !string.IsNullOrWhiteSpace(strategy) && Weights.TryGetValue(strategy, out var weight) ? weight : 1.0d;

    public int ProviderPriorityFor(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && ProviderPriorities.TryGetValue(provider, out var priority) ? priority : 0;

    /// <summary>Strateji sırasındaki index (bilinmiyorsa en sona atar).</summary>
    public int OrderIndexOf(string strategy)
    {
        for (var i = 0; i < StrategyOrder.Count; i++)
        {
            if (string.Equals(StrategyOrder[i], strategy, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }
}
