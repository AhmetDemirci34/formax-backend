using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.MatchIdentity.Abstractions;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Match Identity Engine.
/// Her kriter (strateji) yalnızca kendi HAM skorunu üretir; engine bu skorları merkezi
/// <see cref="MatchIdentityConfiguration"/>'daki AĞIRLIKLARLA çarpıp TOPLAYARAK her kimlik için toplam
/// Confidence hesaplar. En yüksek toplam, config'teki EŞİĞİ geçerse aday o kimliğe ilişkilendirilir;
/// geçmezse yeni bir FORMAX Match ID atanır. Eşik ve ağırlıklar kod içine gömülü değildir.
///
/// KAPSAM DIŞI: Merge / Conflict / Coverage / DB burada YOKTUR. Durumsuzdur: bilinen kimlikler dışarıdan verilir.
/// </summary>
public sealed class MatchIdentityEngine : IMatchIdentityResolver
{
    private readonly IReadOnlyList<IMatchIdentityStrategy> _strategies;
    private readonly MatchIdentityConfiguration _configuration;

    public MatchIdentityEngine(
        IEnumerable<IMatchIdentityStrategy> strategies,
        MatchIdentityConfiguration configuration)
    {
        _strategies = (strategies ?? Enumerable.Empty<IMatchIdentityStrategy>())
            .Where(s => s is not null)
            .ToList();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public MatchIdentityResult Resolve(
        ProviderMatchReference reference,
        IReadOnlyCollection<MatchIdentityResult> known)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        known ??= Array.Empty<MatchIdentityResult>();

        MatchIdentityResult? bestIdentity = null;
        var bestScore = 0d;

        foreach (var identity in known)
        {
            var score = TotalScore(reference, identity);
            if (score > bestScore)
            {
                bestScore = score;
                bestIdentity = identity;
            }
        }

        if (bestIdentity is not null && bestScore >= _configuration.AssociationThreshold)
            return bestIdentity.Associate(reference, bestScore, "composite");

        // Eşik geçilmedi → yeni FORMAX Match ID ata.
        return MatchIdentityResult.NewIdentity(FormaxMatchId.NewSynthetic(), reference);
    }

    /// <summary>Kriter skorlarının AĞIRLIKLI toplamı (toplam Confidence). Ağırlık config'ten gelir.</summary>
    private double TotalScore(ProviderMatchReference reference, MatchIdentityResult identity)
    {
        var total = 0d;
        foreach (var strategy in _strategies)
            total += _configuration.WeightFor(strategy.Name) * strategy.Score(reference, identity);
        return total;
    }
}
