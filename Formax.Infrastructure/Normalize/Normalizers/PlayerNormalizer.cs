using System;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM oyuncu (<see cref="RawPlayer"/>) → <see cref="NormalizedPlayer"/> dönüştürür.
/// Provider-bağımsızdır. İSKELET: gerçek eşleme henüz yazılmadı.
/// </summary>
public sealed class PlayerNormalizer : NormalizerBase<RawPlayer, NormalizedPlayer>
{
    public PlayerNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedPlayer Normalize(RawPlayer raw) =>
        throw new NotImplementedException(
            "PlayerNormalizer iskelet aşamasında; RawPlayer → NormalizedPlayer eşlemesi henüz yazılmadı.");
}
