using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Conflict;
using Formax.Infrastructure.Conflict.Abstractions;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Pipeline.Abstractions;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 5. aşama — Conflict. Merge'in devamıdır.
/// Merge aşamasının <see cref="MergeFieldState.Conflict"/> işaretlediği alanları <see cref="ConflictEngine"/>
/// ile çözer ve sonuçları AYNI <see cref="MergeResult{T}"/>'ı zenginleştirerek (ConflictResolutions)
/// geri yazar. Ayrı bir sonuç/wrapper modeli üretilmez.
///
/// KAPSAM DIŞI: Coverage / DB burada YOKTUR.
/// </summary>
public sealed class ConflictStage : IPipelineStage
{
    private readonly ConflictEngine _conflictEngine;
    private readonly IConflictConfigurationProvider _configurationProvider;

    public ConflictStage(ConflictEngine conflictEngine, IConflictConfigurationProvider configurationProvider)
    {
        _conflictEngine = conflictEngine;
        _configurationProvider = configurationProvider;
    }

    public PipelineStage Stage => PipelineStage.Conflict;

    public Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var mergeResults = context.Get<IReadOnlyList<MergeResult<NormalizedFixture>>>("gdp.merged.fixtures")
            ?? Array.Empty<MergeResult<NormalizedFixture>>();

        var configuration = _configurationProvider.Get();

        // Fixture çatışmaları
        var enrichedFixtures = new List<MergeResult<NormalizedFixture>>(mergeResults.Count);
        foreach (var mergeResult in mergeResults)
        {
            if (mergeResult is null)
                continue;

            enrichedFixtures.Add(mergeResult with { ConflictResolutions = ResolveConflicts(mergeResult, configuration) });
        }

        context.Set("gdp.merged.fixtures", enrichedFixtures);

        // Weather çatışmaları (aynı generic çözüm; Weather'da LastUpdated → ProviderPriority zinciri geçerli)
        var weatherMerges = context.Get<IReadOnlyList<MergeResult<NormalizedWeather>>>("gdp.merged.weather")
            ?? Array.Empty<MergeResult<NormalizedWeather>>();

        var enrichedWeather = new List<MergeResult<NormalizedWeather>>(weatherMerges.Count);
        foreach (var mergeResult in weatherMerges)
        {
            if (mergeResult is null)
                continue;

            enrichedWeather.Add(mergeResult with { ConflictResolutions = ResolveConflicts(mergeResult, configuration) });
        }

        context.Set("gdp.merged.weather", enrichedWeather);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Bir <see cref="MergeResult{T}"/>'ın Conflict işaretli alanlarını çözer (model türünden bağımsız;
    /// alan durumu/provenance non-generic). Conflict, Merge'in devamıdır — çözümler geri yazılır.
    /// </summary>
    private List<ConflictResolution> ResolveConflicts<T>(MergeResult<T> mergeResult, ConflictConfiguration configuration)
    {
        var resolutions = new List<ConflictResolution>();

        foreach (var field in mergeResult.Fields)
        {
            if (field is null || field.State != MergeFieldState.Conflict)
                continue;

            var candidates = field.Provenance
                .Where(c => c is not null && c.Value is not null)
                .Select(c => new ConflictCandidate<object>
                {
                    ProviderName = c.ProviderName,
                    Value = c.Value!,
                    ProviderPriority = configuration.ProviderPriorityFor(c.ProviderName),
                    SourceCount = 1
                })
                .ToList();

            var fieldConflict = new FieldConflict<object> { Field = field.Field, Candidates = candidates };
            resolutions.Add(_conflictEngine.Resolve<object>(fieldConflict));
        }

        return resolutions;
    }
}
