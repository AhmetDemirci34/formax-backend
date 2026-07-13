using System;

namespace Formax.Infrastructure.MatchIdentity.Strategies;

/// <summary>
/// İsim karşılaştırma yardımcısı. Karşılaştırılan değerler zaten Normalize aşamasında TextNormalizer'dan
/// geçmiştir (NormalizedFixture); burada yalnızca trim + büyük/küçük harf duyarsız eşitlik uygulanır.
/// </summary>
internal static class MatchIdentityText
{
    public static bool NameEquals(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
