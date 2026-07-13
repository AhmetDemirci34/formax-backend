using System;
using Formax.Infrastructure.MatchIdentity.Abstractions;

namespace Formax.Infrastructure.MatchIdentity.Strategies;

/// <summary>
/// Başlama zamanı kriteri. Aday ile mevcut kimliğin referanslarından herhangi birinin UTC başlama zamanı
/// ±5 dakika tolerans içindeyse ağırlık kadar skor katkısı verir.
/// </summary>
public sealed class KickoffTimeStrategy : IMatchIdentityStrategy
{
    // Eşleşme toleransı kriterin kendi mantığıdır (ağırlık değildir); ağırlık config'ten gelir.
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public string Name => "KickoffTime";

    // Yalnızca ham skor (±5dk içinde = 1.0). Ağırlıklandırma engine'de config'ten uygulanır.
    public double Score(ProviderMatchReference candidate, MatchIdentityResult existing)
    {
        if (candidate is null || existing is null || candidate.KickoffUtc is not DateTimeOffset candidateKickoff)
            return 0d;

        foreach (var reference in existing.References)
        {
            if (reference?.KickoffUtc is DateTimeOffset referenceKickoff
                && (candidateKickoff - referenceKickoff).Duration() <= Tolerance)
                return 1.0d;
        }

        return 0d;
    }
}
