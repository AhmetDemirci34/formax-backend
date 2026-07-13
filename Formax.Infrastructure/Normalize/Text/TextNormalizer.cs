using System.Globalization;
using System.Text;

namespace Formax.Infrastructure.Normalize.Text;

/// <summary>
/// İsim/metin temizleme altyapısı (saf, durumsuz).
/// Trim, iç boşluk sadeleştirme, kontrol/görünmez karakter temizliği ve büyük/küçük harf düzeni sağlar.
/// </summary>
public static class TextNormalizer
{
    /// <summary>
    /// Metni temizler: Unicode FormC'ye getirir, kontrol/format (görünmez) karakterleri atar,
    /// ardışık boşlukları tek boşluğa indirger ve baş/son boşlukları kırpar.
    /// Null/boş girdi için boş string döner.
    /// </summary>
    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var ch in value.Normalize(NormalizationForm.FormC))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
                continue;

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>Temizlenmiş metni görüntüleme için başlık düzenine getirir (kültür-duyarlı).</summary>
    public static string ToTitleCase(string? value, CultureInfo? culture = null)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0)
            return string.Empty;

        culture ??= CultureInfo.InvariantCulture;
        return culture.TextInfo.ToTitleCase(cleaned.ToLower(culture));
    }

    /// <summary>Aksan/diakritik işaretlerini kaldırır (eşleşme/arama hazırlığı için). Temizlemeyi de uygular.</summary>
    public static string RemoveDiacritics(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0)
            return string.Empty;

        var decomposed = cleaned.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
