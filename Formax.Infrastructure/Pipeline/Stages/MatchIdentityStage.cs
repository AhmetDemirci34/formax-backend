using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.MatchIdentity;
using Formax.Infrastructure.MatchIdentity.Abstractions;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Pipeline.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 3. aşama — Match Identity.
/// Normalize aşamasının ürettiği fikstürlerden provider referansları kurar ve
/// <see cref="IMatchIdentityResolver"/> ile aynı maçı temsil edenleri tek FORMAX Match ID altında toplar;
/// eşiği geçmeyenlere yeni kimlik atar.
///
/// KALICI KİMLİK (çalıştırmalar arası idempotency): resolver in-batch eşleşme bulamayıp YENİ (sentetik)
/// kimlik ürettiğinde, önce mevcut <see cref="Formax.Infrastructure.Persistence.GdpProviderMatchReference"/>
/// kaydına bakılır — bu (ProviderName + ProviderMatchId) daha önce persist edildiyse var olan FormaxMatchId
/// yeniden kullanılır. Böylece aynı gerçek maç ikinci çalıştırmada YENİDEN eklenmez (duplicate önlenir).
///
/// Not: <see cref="NormalizedFixture"/> provider adı taşımaz; ham (<see cref="RawFixture"/>) ile normalize
/// listesi index hizasıyla eşlenir (NormalizeStage birebir sırada üretir).
///
/// KAPSAM DIŞI: Merge / Conflict / Coverage burada YOKTUR. (DB yalnızca kalıcı kimlik ÇÖZÜMÜ için okunur.)
/// </summary>
public sealed class MatchIdentityStage : IPipelineStage
{
    private readonly IMatchIdentityResolver _resolver;
    private readonly FormaxDbContext _db;

    public MatchIdentityStage(IMatchIdentityResolver resolver, FormaxDbContext db)
    {
        _resolver = resolver;
        _db = db;
    }

    public PipelineStage Stage => PipelineStage.MatchIdentity;

    public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var rawFixtures = context.Get<IReadOnlyList<RawFixture>>("gdp.rawmodel.fixtures")
            ?? Array.Empty<RawFixture>();
        var normalizedFixtures = context.Get<IReadOnlyList<NormalizedFixture>>("gdp.normalized.fixtures")
            ?? Array.Empty<NormalizedFixture>();

        var identities = new List<MatchIdentityResult>();
        var count = Math.Min(rawFixtures.Count, normalizedFixtures.Count);

        for (var i = 0; i < count; i++)
        {
            var reference = BuildReference(rawFixtures[i], normalizedFixtures[i]);
            if (reference is null)
                continue;

            var result = _resolver.Resolve(reference, identities);

            // KALICI KİMLİK: yeni (sentetik) kimlik üretildiyse, bu provider maçı DB'de zaten var mı?
            // Varsa mevcut FormaxMatchId'yi yeniden kullan → ikinci çalıştırmada duplicate oluşmaz.
            if (result.IsNew && !string.IsNullOrWhiteSpace(reference.ProviderMatchId))
            {
                var existingId = await _db.GdpProviderMatchReferences
                    .AsNoTracking()
                    .Where(r => r.ProviderName == reference.ProviderName
                                && r.ProviderMatchId == reference.ProviderMatchId)
                    .Select(r => r.FormaxMatchId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(existingId))
                    result = result with { FormaxMatchId = FormaxMatchId.From(existingId) };
            }

            if (result.IsNew)
            {
                identities.Add(result);
            }
            else
            {
                var index = identities.FindIndex(id => id.FormaxMatchId.Equals(result.FormaxMatchId));
                if (index >= 0)
                    identities[index] = result;
                else
                    identities.Add(result);
            }
        }

        context.Set("gdp.identities", identities);
    }

    private static ProviderMatchReference? BuildReference(RawFixture raw, NormalizedFixture normalized)
    {
        if (raw is null || normalized is null)
            return null;

        return new ProviderMatchReference
        {
            ProviderName = raw.ProviderName ?? string.Empty,
            ProviderMatchId = normalized.ProviderMatchId ?? string.Empty,
            HomeTeam = normalized.HomeTeam?.Name,
            AwayTeam = normalized.AwayTeam?.Name,
            KickoffUtc = normalized.KickoffUtc,
            Competition = normalized.Competition?.Name,
            Country = null
        };
    }
}
