using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Formax.Infrastructure.Historical;

/// <summary>
/// Tarihsel takım/kulüp adı normalizasyonu (tekilleştirme anahtarı üretir). Saf, IO'suz.
/// Sıra: trim → küçük harf → diakritik sadeleştir → noktalama kaldır (apostrof/nokta/tire) →
/// boşluk sadeleştir → ALIAS map uygula. Böylece "Nott'm Forest" == "Nottm Forest".
///
/// NOT: <see cref="Aliases"/>, normalizasyonun çözemediği bilinen eşadlar içindir (ör. "Spurs"→
/// "Tottenham"). "Önceki analizdeki 4 alias kuralı" belgelenmediğinden bu map GENİŞLETİLEBİLİR
/// bırakıldı — kanonik kurallar verildiğinde buraya eklenir; başka hiçbir yer değişmez.
/// </summary>
public static class HistoricalNameNormalizer
{
    /// <summary>Normalize edilmiş-anahtar → kanonik normalize-anahtar eşadları (genişletilebilir).</summary>
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        // Kanonik 4 alias kuralı verildiğinde buraya eklenecek. Örn (pasif):
        // ["spurs"] = "tottenham",
    };

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var lowered = name.Trim().ToLowerInvariant();

        // Diakritik sadeleştirme (Unicode ayrıştır + non-spacing mark at).
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;

            // Türkçe/özel eşleme + noktalama kaldırma.
            switch (ch)
            {
                case 'ı': sb.Append('i'); break;
                case 'ş': sb.Append('s'); break;
                case 'ğ': sb.Append('g'); break;
                case 'ç': sb.Append('c'); break;
                case 'ö': sb.Append('o'); break;
                case 'ü': sb.Append('u'); break;
                case '\'': case '’': case '.': case '-': case '/': break; // noktalama at
                case ' ': sb.Append(' '); break;
                default:
                    if (char.IsLetterOrDigit(ch) || ch == ' ')
                        sb.Append(ch);
                    break;
            }
        }

        // Çoklu boşlukları teke indir.
        var collapsed = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "\\s+", " ").Trim();

        return Aliases.TryGetValue(collapsed, out var canonical) ? canonical : collapsed;
    }
}
