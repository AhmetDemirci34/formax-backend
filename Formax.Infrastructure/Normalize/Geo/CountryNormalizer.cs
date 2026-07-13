using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Text;

namespace Formax.Infrastructure.Normalize.Geo;

/// <summary>
/// Ülke normalize iskeleti. Şimdilik yalnızca adı temizler; kanonik eşleme ve ISO kod çözümü
/// (ör. "England" ↔ "İngiltere" ↔ "GB-ENG") sonraki fazda buraya eklenecek.
/// </summary>
public sealed class CountryNormalizer : ICountryNormalizer
{
    public NormalizedCountry Normalize(string? rawCountryOrCode)
    {
        var name = TextNormalizer.Clean(rawCountryOrCode);

        // İSKELET: kanonik ad/ISO kod tablosu henüz yok → Code null bırakılır.
        return new NormalizedCountry { Name = name };
    }
}
