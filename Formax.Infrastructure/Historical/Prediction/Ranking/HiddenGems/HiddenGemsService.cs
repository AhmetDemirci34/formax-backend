using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.HiddenGems;

/// <summary>Discovery Feed'deki maçları analiz edip Hidden Gem'leri tespit eden servis.</summary>
public interface IHiddenGemsService
{
    /// <summary>Yalnız Hidden Gem olarak tespit edilen maçlar (HiddenGemScore azalan). Discovery/Radar'ı değiştirmez.</summary>
    Task<IReadOnlyList<HiddenGemAnalysis>> FindHiddenGemsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);

    /// <summary>Tüm maçların gem analizi (flag'li), Discovery sırasında.</summary>
    Task<IReadOnlyList<HiddenGemAnalysis>> AnalyzeAllAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IHiddenGemsService"/>
public sealed class HiddenGemsService : IHiddenGemsService
{
    private readonly IRadarContextBuilder _contextBuilder;
    private readonly IHiddenGemsEngine _engine;
    private readonly HiddenGemWeights _weights;

    public HiddenGemsService(IRadarContextBuilder contextBuilder, IHiddenGemsEngine engine, HiddenGemWeights weights)
    {
        _contextBuilder = contextBuilder;
        _engine = engine;
        _weights = weights;
    }

    public async Task<IReadOnlyList<HiddenGemAnalysis>> AnalyzeAllAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var contexts = await _contextBuilder.BuildContextsAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);
        return contexts.Select(c => _engine.Analyze(c, _weights)).ToList();
    }

    public async Task<IReadOnlyList<HiddenGemAnalysis>> FindHiddenGemsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var all = await AnalyzeAllAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);
        return all.Where(a => a.IsHiddenGem)
            .OrderByDescending(a => a.HiddenGemScore)
            .ThenBy(a => a.MatchId)
            .ToList();
    }
}
