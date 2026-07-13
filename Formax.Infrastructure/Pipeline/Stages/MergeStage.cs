using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.MatchIdentity;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Pipeline.Abstractions;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 4. aşama — Merge.
/// Her FORMAX Match kimliği için, ona bağlı provider referanslarına karşılık gelen
/// <see cref="NormalizedFixture"/>'ları toplar ve <see cref="MergeEngine"/> ile ALAN BAZLI tek sonuca
/// birleştirir (eksik doldurma; aynı değer tek kopya; farklı değer → Conflict Engine'e bırakılır).
///
/// KAPSAM DIŞI: Conflict çözme / Coverage / DB burada YOKTUR.
/// </summary>
public sealed class MergeStage : IPipelineStage
{
    private readonly MergeEngine _mergeEngine;

    public MergeStage(MergeEngine mergeEngine)
    {
        _mergeEngine = mergeEngine;
    }

    public PipelineStage Stage => PipelineStage.Merge;

    public Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var identities = context.Get<IReadOnlyList<MatchIdentityResult>>("gdp.identities")
            ?? Array.Empty<MatchIdentityResult>();
        var rawFixtures = context.Get<IReadOnlyList<RawFixture>>("gdp.rawmodel.fixtures")
            ?? Array.Empty<RawFixture>();
        var normalizedFixtures = context.Get<IReadOnlyList<NormalizedFixture>>("gdp.normalized.fixtures")
            ?? Array.Empty<NormalizedFixture>();

        // (provider + providerMatchId) → NormalizedFixture. Ham liste provider adını, normalize liste
        // ProviderMatchId'yi sağlar; ikisi NormalizeStage tarafından birebir sırada üretilir.
        var byKey = BuildLookup(rawFixtures, normalizedFixtures);

        var mergeResults = new List<MergeResult<NormalizedFixture>>();

        foreach (var identity in identities)
        {
            if (identity is null)
                continue;

            var contributions = new List<MergeContribution<NormalizedFixture>>();
            foreach (var reference in identity.References)
            {
                if (reference is null)
                    continue;

                var key = Key(reference.ProviderName, reference.ProviderMatchId);
                if (byKey.TryGetValue(key, out var fixture) && fixture is not null)
                {
                    contributions.Add(new MergeContribution<NormalizedFixture>
                    {
                        ProviderName = reference.ProviderName,
                        Data = fixture,
                        Priority = 0
                    });
                }
            }

            if (contributions.Count == 0)
                continue;

            var mergeContext = new MergeContext { FormaxMatchId = identity.FormaxMatchId.Value };
            mergeResults.Add(_mergeEngine.Merge<NormalizedFixture>(contributions, mergeContext));
        }

        context.Set("gdp.merged.fixtures", mergeResults);

        // ---- Weather merge (maç-eksenli; identity YOK) ----
        // Bir pipeline çalıştırması = TEK maç = TEK istenen koordinat. Bu yüzden o maça ait TÜM weather
        // sağlayıcıları TEK grupta birleşir (C1 düzeltmesi: provider'ın döndürdüğü grid koordinatına göre
        // gruplama YAPILMAZ — aksi halde Open-Meteo 48.14 vs MET Norway 48.13 ayrı gruplara düşer, birleşmez
        // ve maç başına duplicate satır oluşurdu). Kanonik koordinat isteğden gelir (Persist orada kullanır);
        // merge yalnızca ölçümleri (sıcaklık/durum/tahmin) uzlaştırır.
        var rawWeather = context.Get<IReadOnlyList<RawWeather>>("gdp.rawmodel.weather")
            ?? Array.Empty<RawWeather>();
        var normalizedWeather = context.Get<IReadOnlyList<NormalizedWeather>>("gdp.normalized.weather")
            ?? Array.Empty<NormalizedWeather>();

        var weatherContributions = new List<MergeContribution<NormalizedWeather>>();
        var weatherCount = Math.Min(rawWeather.Count, normalizedWeather.Count);
        for (var i = 0; i < weatherCount; i++)
        {
            var raw = rawWeather[i];
            var normalized = normalizedWeather[i];
            if (raw is null || normalized is null)
                continue;

            weatherContributions.Add(new MergeContribution<NormalizedWeather>
            {
                ProviderName = raw.ProviderName ?? string.Empty,
                Data = normalized,
                Priority = 0
            });
        }

        var weatherMerges = new List<MergeResult<NormalizedWeather>>();
        if (weatherContributions.Count > 0)
        {
            var mergeContext = new MergeContext { FormaxMatchId = context.FormaxMatchId };
            weatherMerges.Add(_mergeEngine.Merge<NormalizedWeather>(weatherContributions, mergeContext));
        }

        context.Set("gdp.merged.weather", weatherMerges);
        return Task.CompletedTask;
    }

    private static Dictionary<string, NormalizedFixture> BuildLookup(
        IReadOnlyList<RawFixture> rawFixtures,
        IReadOnlyList<NormalizedFixture> normalizedFixtures)
    {
        var lookup = new Dictionary<string, NormalizedFixture>(StringComparer.Ordinal);
        var count = Math.Min(rawFixtures.Count, normalizedFixtures.Count);

        for (var i = 0; i < count; i++)
        {
            var raw = rawFixtures[i];
            var normalized = normalizedFixtures[i];
            if (raw is null || normalized is null)
                continue;

            lookup[Key(raw.ProviderName, normalized.ProviderMatchId)] = normalized;
        }

        return lookup;
    }

    private static string Key(string? providerName, string? providerMatchId) =>
        $"{(providerName ?? string.Empty).Trim().ToLowerInvariant()}::{providerMatchId ?? string.Empty}";
}
