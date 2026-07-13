using System;
using Formax.Infrastructure.MatchIdentity.Abstractions;

namespace Formax.Infrastructure.MatchIdentity.Strategies;

/// <summary>
/// Provider ID kriteri (en güçlü sinyal).
/// AYNI provider + AYNI ProviderMatchId → kesin eşleşme (skor 1.0, tek başına eşiği geçer = Exact Match).
/// FARKLI provider'da ProviderMatchId dikkate ALINMAZ (katkı 0).
/// </summary>
public sealed class ProviderIdStrategy : IMatchIdentityStrategy
{
    public string Name => "ProviderId";

    public double Score(ProviderMatchReference candidate, MatchIdentityResult existing)
    {
        if (candidate is null || existing is null || string.IsNullOrWhiteSpace(candidate.ProviderMatchId))
            return 0d;

        foreach (var reference in existing.References)
        {
            if (reference is null || string.IsNullOrWhiteSpace(reference.ProviderMatchId))
                continue;

            var sameProvider = string.Equals(reference.ProviderName, candidate.ProviderName, StringComparison.OrdinalIgnoreCase);
            var sameId = string.Equals(reference.ProviderMatchId, candidate.ProviderMatchId, StringComparison.Ordinal);

            if (sameProvider && sameId)
                return 1.0d;
        }

        return 0d;
    }
}
