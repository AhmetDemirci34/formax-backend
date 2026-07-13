using Formax.Infrastructure.MatchIdentity.Abstractions;

namespace Formax.Infrastructure.MatchIdentity.Strategies;

/// <summary>
/// Organizasyon/Lig kriteri. Aday ile mevcut kimliğin referanslarından herhangi birinin lig adı
/// (TextNormalizer'dan geçmiş değerlerle) eşleşirse ağırlık kadar skor katkısı verir.
/// Tek başına belirleyici değildir; diğer kriterleri güçlendirir.
/// </summary>
public sealed class CompetitionStrategy : IMatchIdentityStrategy
{
    public string Name => "Competition";

    // Yalnızca ham skor (eşleşme = 1.0). Ağırlıklandırma engine'de config'ten uygulanır.
    public double Score(ProviderMatchReference candidate, MatchIdentityResult existing)
    {
        if (candidate is null || existing is null || string.IsNullOrWhiteSpace(candidate.Competition))
            return 0d;

        foreach (var reference in existing.References)
        {
            if (reference is not null && MatchIdentityText.NameEquals(candidate.Competition, reference.Competition))
                return 1.0d;
        }

        return 0d;
    }
}
