using Formax.Infrastructure.MatchIdentity.Abstractions;

namespace Formax.Infrastructure.MatchIdentity.Strategies;

/// <summary>
/// Deplasman takım kriteri. Aday ile mevcut kimliğin referanslarından herhangi birinin deplasman adı
/// (TextNormalizer'dan geçmiş değerlerle) eşleşirse ağırlık kadar skor katkısı verir.
/// </summary>
public sealed class AwayTeamStrategy : IMatchIdentityStrategy
{
    public string Name => "AwayTeam";

    // Yalnızca ham skor (eşleşme = 1.0). Ağırlıklandırma engine'de config'ten uygulanır.
    public double Score(ProviderMatchReference candidate, MatchIdentityResult existing)
    {
        if (candidate is null || existing is null || string.IsNullOrWhiteSpace(candidate.AwayTeam))
            return 0d;

        foreach (var reference in existing.References)
        {
            if (reference is not null && MatchIdentityText.NameEquals(candidate.AwayTeam, reference.AwayTeam))
                return 1.0d;
        }

        return 0d;
    }
}
