using System;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM lig/turnuva (<see cref="RawCompetition"/>) → <see cref="NormalizedCompetition"/> dönüştürür.
/// Provider-bağımsızdır. İSKELET: gerçek eşleme henüz yazılmadı.
/// </summary>
public sealed class CompetitionNormalizer : NormalizerBase<RawCompetition, NormalizedCompetition>
{
    public CompetitionNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedCompetition Normalize(RawCompetition raw) =>
        throw new NotImplementedException(
            "CompetitionNormalizer iskelet aşamasında; RawCompetition → NormalizedCompetition eşlemesi henüz yazılmadı.");
}
