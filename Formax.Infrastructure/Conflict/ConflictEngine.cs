using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Conflict Engine giriş noktası.
/// Bir alanın aday değerlerini, kayıtlı <see cref="IConflictResolver{T}"/> stratejileriyle
/// <see cref="ConflictConfiguration.StrategyOrder"/> sırasına göre çözer. Her stratejinin ham
/// confidence'ı, config'teki AĞIRLIKLA çarpılır; sonuç <see cref="ConflictConfiguration.MinimumConfidence"/>
/// eşiğini geçen İLK strateji kazanır. Hiçbiri geçemezse Conflict durumu korunur (veri kaybı yok;
/// tüm adaylar sonuçta saklanır).
///
/// KAPSAM DIŞI: Coverage / DB burada YOKTUR. Ağırlık/eşik/sıra kod içinde değil, config'ten gelir.
/// </summary>
public sealed class ConflictEngine
{
    private readonly IServiceProvider _services;
    private readonly IConflictConfigurationProvider _configurationProvider;

    public ConflictEngine(IServiceProvider services, IConflictConfigurationProvider configurationProvider)
    {
        _services = services;
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    public ConflictResolution Resolve<T>(FieldConflict<T> conflict)
    {
        if (conflict is null) throw new ArgumentNullException(nameof(conflict));

        var configuration = _configurationProvider.Get();
        var candidates = conflict.Candidates ?? Array.Empty<ConflictCandidate<T>>();
        var boxedCandidates = candidates.Where(c => c is not null).Select(Box).ToList();

        if (boxedCandidates.Count == 0)
            return ConflictResolution.Unresolved(conflict.Field, boxedCandidates);

        var resolvers = _services.GetServices<IConflictResolver<T>>()
            .Where(r => r is not null)
            .OrderBy(r => configuration.OrderIndexOf(r.Name))
            .ToList();

        foreach (var resolver in resolvers)
        {
            var decision = resolver.Resolve(candidates);
            if (decision is null || decision.Selected is null)
                continue;

            var weightedConfidence = configuration.WeightFor(resolver.Name) * decision.Confidence;
            if (weightedConfidence >= configuration.MinimumConfidence)
                return ConflictResolution.Resolved(conflict.Field, Box(decision.Selected), resolver.Name, weightedConfidence, boxedCandidates);
        }

        // Hiçbir strateji kesin karar veremedi → Conflict korunur.
        return ConflictResolution.Unresolved(conflict.Field, boxedCandidates);
    }

    /// <summary>Bu tür için en az bir çözüm stratejisi kayıtlı mı?</summary>
    public bool CanResolve<T>() => _services.GetServices<IConflictResolver<T>>().Any();

    private static ConflictCandidate<object> Box<T>(ConflictCandidate<T> candidate) => new()
    {
        ProviderName = candidate.ProviderName,
        Value = candidate.Value!,
        ProviderPriority = candidate.ProviderPriority,
        ProviderConfidence = candidate.ProviderConfidence,
        LastUpdatedUtc = candidate.LastUpdatedUtc,
        SourceCount = candidate.SourceCount,
        IsManualOverride = candidate.IsManualOverride
    };
}
