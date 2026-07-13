using Formax.Infrastructure.Normalize.Models;

namespace Formax.Infrastructure.Normalize.Geo;

/// <summary>
/// Ham ülke adı/kodunu ortak <see cref="NormalizedCountry"/> modeline dönüştüren sözleşme.
/// Kanonik ad eşleme ve ISO kod tablosu sonraki fazlarda bu implementasyona eklenecek.
/// (Lig/turnuva adları da aynı desenle CompetitionNormalizer üzerinden kanonikleştirilecek.)
/// </summary>
public interface ICountryNormalizer
{
    NormalizedCountry Normalize(string? rawCountryOrCode);
}
