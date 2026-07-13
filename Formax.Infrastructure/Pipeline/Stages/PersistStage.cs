using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.MatchIdentity;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Persistence;
using Formax.Infrastructure.Persistence.H2H;
using Formax.Infrastructure.Persistence.Standings;
using Formax.Infrastructure.Pipeline.Abstractions;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// Son aşama — Database Persist.
/// Her FORMAX Match kimliği için son <see cref="MergeResult{T}"/>'ı (Value + durumlar + çözümler) ve
/// provider referanslarını <see cref="IGdpMatchPersister"/> ile veritabanına yazar (insert/update/unchanged).
/// Idempotent + transaction'lı; her maç sonucu <see cref="GdpPersistOutcome"/> olarak toplanır.
/// </summary>
public sealed class PersistStage : IPipelineStage
{
    private readonly IGdpMatchPersister _persister;
    private readonly IGdpWeatherPersister _weatherPersister;
    private readonly IGdpH2HPersister _h2hPersister;
    private readonly IGdpStandingsPersister _standingsPersister;

    public PersistStage(
        IGdpMatchPersister persister,
        IGdpWeatherPersister weatherPersister,
        IGdpH2HPersister h2hPersister,
        IGdpStandingsPersister standingsPersister)
    {
        _persister = persister;
        _weatherPersister = weatherPersister;
        _h2hPersister = h2hPersister;
        _standingsPersister = standingsPersister;
    }

    public PipelineStage Stage => PipelineStage.Persist;

    public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var identities = context.Get<IReadOnlyList<MatchIdentityResult>>("gdp.identities")
            ?? Array.Empty<MatchIdentityResult>();
        var mergeResults = context.Get<IReadOnlyList<MergeResult<NormalizedFixture>>>("gdp.merged.fixtures")
            ?? Array.Empty<MergeResult<NormalizedFixture>>();

        var outcomes = new List<GdpPersistOutcome>();

        // Merge sonuçları, identity'lerle birebir sırada üretildi (Merge/Conflict aşamaları korudu).
        var count = Math.Min(identities.Count, mergeResults.Count);
        for (var i = 0; i < count; i++)
        {
            var identity = identities[i];
            var mergeResult = mergeResults[i];
            if (identity is null || mergeResult is null)
                continue;

            var request = new GdpPersistRequest
            {
                FormaxMatchId = identity.FormaxMatchId.Value,
                Merge = mergeResult,
                ProviderReferences = identity.References
                    .Where(r => r is not null)
                    .Select(r => new GdpProviderReferenceInput
                    {
                        ProviderName = r.ProviderName,
                        ProviderMatchId = r.ProviderMatchId
                    })
                    .ToList()
            };

            outcomes.Add(await _persister.PersistAsync(request, cancellationToken).ConfigureAwait(false));
        }

        context.Set("gdp.persist.outcomes", outcomes);

        // ---- Weather persist (Canonical Domain MatchWeather; kanonik koordinat gerçek Match'ten) ----
        var weatherMerges = context.Get<IReadOnlyList<MergeResult<NormalizedWeather>>>("gdp.merged.weather")
            ?? Array.Empty<MergeResult<NormalizedWeather>>();

        // Kanonik koordinat + FormaxMatchId, ProviderStage'in çözdüğü istekten alınır (echoed grid değil).
        var resolvedRequest = context.Get<ProviderRequest>("gdp.request");
        var formaxMatchId = resolvedRequest?.FormaxMatchId ?? context.FormaxMatchId;

        var weatherOutcomes = new List<GdpWeatherPersistOutcome>(weatherMerges.Count);
        foreach (var mergeResult in weatherMerges)
        {
            if (mergeResult is null)
                continue;

            var weatherRequest = new GdpWeatherPersistRequest
            {
                FormaxMatchId = formaxMatchId,
                Latitude = resolvedRequest?.Latitude,
                Longitude = resolvedRequest?.Longitude,
                Merge = mergeResult
            };

            weatherOutcomes.Add(await _weatherPersister.PersistAsync(weatherRequest, cancellationToken).ConfigureAwait(false));
        }

        context.Set("gdp.persist.weather.outcomes", weatherOutcomes);

        // ---- H2H persist (Canonical Domain HeadToHead; canonical Match geçmişinden türetilir) ----
        // Provider'dan gelmez → Merge/Conflict yolu yok; mevcut maçın Domain Id'siyle türetilir.
        if (context.MatchId is int h2hMatchId && !string.IsNullOrWhiteSpace(formaxMatchId))
        {
            var h2hOutcome = await _h2hPersister
                .PersistAsync(new GdpH2HPersistRequest { FormaxMatchId = formaxMatchId, MatchId = h2hMatchId }, cancellationToken)
                .ConfigureAwait(false);
            context.Set("gdp.persist.h2h.outcome", h2hOutcome);
        }

        // ---- Standings persist (Canonical Domain CompetitionStanding; competition maçlarından türetilir) ----
        var competitionName = resolvedRequest?.Competition;
        if (!string.IsNullOrWhiteSpace(competitionName))
        {
            var standingsOutcome = await _standingsPersister
                .PersistAsync(new GdpStandingsPersistRequest { CompetitionName = competitionName! }, cancellationToken)
                .ConfigureAwait(false);
            context.Set("gdp.persist.standings.outcome", standingsOutcome);
        }
    }
}
