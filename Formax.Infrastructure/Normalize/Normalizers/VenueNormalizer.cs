using System;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM stat/mekan (<see cref="RawVenue"/>) → <see cref="NormalizedVenue"/> dönüştürür.
/// Provider-bağımsızdır. İSKELET: gerçek eşleme henüz yazılmadı.
/// </summary>
public sealed class VenueNormalizer : NormalizerBase<RawVenue, NormalizedVenue>
{
    public VenueNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedVenue Normalize(RawVenue raw) =>
        throw new NotImplementedException(
            "VenueNormalizer iskelet aşamasında; RawVenue → NormalizedVenue eşlemesi henüz yazılmadı.");
}
