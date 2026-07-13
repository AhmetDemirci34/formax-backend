using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Normalize;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Pipeline.Abstractions;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 2. aşama — Normalize. İki adım:
///  1) Provider Mapper: Provider Stage'in yazdığı HAM JSON sonuçları → ortak <see cref="RawFixture"/> listesi.
///  2) Normalize: <see cref="RawFixture"/> → <see cref="NormalizedFixture"/> (provider-bağımsız, tek engine).
///
/// Başarısız / mapper'sız provider sonuçları atlanır; sahte veri üretilmez.
/// </summary>
public sealed class NormalizeStage : IPipelineStage
{
    private readonly IReadOnlyList<IProviderMapper<RawFixture>> _fixtureMappers;
    private readonly IReadOnlyList<IProviderMapper<RawWeather>> _weatherMappers;
    private readonly NormalizeEngine _normalizeEngine;

    public NormalizeStage(
        IEnumerable<IProviderMapper<RawFixture>> fixtureMappers,
        IEnumerable<IProviderMapper<RawWeather>> weatherMappers,
        NormalizeEngine normalizeEngine)
    {
        _fixtureMappers = (fixtureMappers ?? Enumerable.Empty<IProviderMapper<RawFixture>>())
            .Where(m => m is not null)
            .ToList();
        _weatherMappers = (weatherMappers ?? Enumerable.Empty<IProviderMapper<RawWeather>>())
            .Where(m => m is not null)
            .ToList();
        _normalizeEngine = normalizeEngine;
    }

    public PipelineStage Stage => PipelineStage.Normalize;

    public Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        // 1) Provider ham JSON → ortak RawFixture listesi
        var rawFixtures = new List<RawFixture>();
        var rawResults = context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.fixtures");
        if (rawResults is not null)
        {
            foreach (var result in rawResults)
            {
                if (result is null || !result.Success)
                    continue;

                var mapper = FindMapper(result.ProviderName);
                if (mapper is null)
                    continue;

                rawFixtures.AddRange(mapper.Map(result.Payload));
            }
        }

        context.Set("gdp.rawmodel.fixtures", rawFixtures);

        // 2) RawFixture → NormalizedFixture (provider-bağımsız)
        var normalizedFixtures = new List<NormalizedFixture>(rawFixtures.Count);
        foreach (var raw in rawFixtures)
        {
            if (raw is null)
                continue;

            normalizedFixtures.Add(_normalizeEngine.Normalize<RawFixture, NormalizedFixture>(raw));
        }

        context.Set("gdp.normalized.fixtures", normalizedFixtures);

        // ---- Weather (koordinat-eksenli; fixture'dan bağımsız paralel dikey) ----
        // 1) Provider ham JSON → ortak RawWeather listesi
        var rawWeather = new List<RawWeather>();
        var rawWeatherResults = context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.weather");
        if (rawWeatherResults is not null)
        {
            foreach (var result in rawWeatherResults)
            {
                if (result is null || !result.Success)
                    continue;

                var mapper = FindWeatherMapper(result.ProviderName);
                if (mapper is null)
                    continue;

                rawWeather.AddRange(mapper.Map(result.Payload));
            }
        }

        context.Set("gdp.rawmodel.weather", rawWeather);

        // 2) RawWeather → NormalizedWeather (provider-bağımsız)
        var normalizedWeather = new List<NormalizedWeather>(rawWeather.Count);
        foreach (var raw in rawWeather)
        {
            if (raw is null)
                continue;

            normalizedWeather.Add(_normalizeEngine.Normalize<RawWeather, NormalizedWeather>(raw));
        }

        context.Set("gdp.normalized.weather", normalizedWeather);
        return Task.CompletedTask;
    }

    private IProviderMapper<RawFixture>? FindMapper(string providerName) =>
        _fixtureMappers.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

    private IProviderMapper<RawWeather>? FindWeatherMapper(string providerName) =>
        _weatherMappers.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
}
