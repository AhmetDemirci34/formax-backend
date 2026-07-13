using System;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM takım (<see cref="RawTeam"/>) → <see cref="NormalizedTeam"/> dönüştürür.
/// Provider-bağımsızdır. İSKELET: gerçek eşleme henüz yazılmadı.
/// </summary>
public sealed class TeamNormalizer : NormalizerBase<RawTeam, NormalizedTeam>
{
    public TeamNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedTeam Normalize(RawTeam raw) =>
        throw new NotImplementedException(
            "TeamNormalizer iskelet aşamasında; RawTeam → NormalizedTeam eşlemesi henüz yazılmadı.");
}
