using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Merkezi Match Identity yapılandırması: eşleştirme eşiği ve kriter ağırlıkları.
/// Bu değerler kod içine (engine/strategy) gömülü DEĞİLDİR; buradan okunur.
/// İleride appsettings / database / admin panelinden beslenebilir (yalnızca bu kaydın kaynağı değişir).
/// </summary>
public sealed class MatchIdentityConfiguration
{
    /// <summary>Bir referansı mevcut kimliğe bağlamak için gereken asgari toplam (ağırlıklı) skor.</summary>
    public double AssociationThreshold { get; init; } = 0.70d;

    /// <summary>Kriter (strateji adı) → ağırlık eşlemesi.</summary>
    public IReadOnlyDictionary<string, double> Weights { get; init; } = DefaultWeights;

    private static readonly IReadOnlyDictionary<string, double> DefaultWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProviderId"] = 1.00d,
            ["HomeTeam"] = 0.25d,
            ["AwayTeam"] = 0.25d,
            ["KickoffTime"] = 0.30d,
            ["Competition"] = 0.20d
        };

    /// <summary>Bir kriterin ağırlığını verir; tanımsızsa 0 (katkısız).</summary>
    public double WeightFor(string strategyName) =>
        !string.IsNullOrWhiteSpace(strategyName) && Weights.TryGetValue(strategyName, out var weight)
            ? weight
            : 0d;
}
