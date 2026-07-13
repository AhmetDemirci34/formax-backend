using Formax.Infrastructure.Normalize.Abstractions;
using Formax.Infrastructure.Normalize.Geo;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Normalizer'lar için ortak temel. Paylaşılan yardımcıları (ülke çözümü) sağlar;
/// isim/tarih temizliği <see cref="Text.TextNormalizer"/> ve <see cref="Time.DateTimeNormalizer"/>
/// üzerinden statik erişilir. Somut normalizer'lar yalnızca HAM → Normalized eşleme mantığını doldurur.
/// </summary>
public abstract class NormalizerBase<TRaw, TModel> : INormalizer<TRaw, TModel>
{
    protected ICountryNormalizer Country { get; }

    protected NormalizerBase(ICountryNormalizer country)
    {
        Country = country;
    }

    public abstract TModel Normalize(TRaw raw);
}
