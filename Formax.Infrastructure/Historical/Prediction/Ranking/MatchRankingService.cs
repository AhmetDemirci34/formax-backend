using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>
/// FORMAX çekirdek motoru: "Bugün hangi maçı izlemeliyim?" — verilen maçları Radar Score'a göre sıralar.
/// Her maç için RadarInput kurar (<see cref="IRadarInputBuilder"/>), skorlar (<see cref="IRadarScoreEngine"/>)
/// ve azalan Radar Score sırasıyla döner. Ağırlıklar config-driven (<see cref="RadarWeights"/>). Deterministik:
/// aynı maç kümesi + aynı model + aynı ağırlıklar → aynı sıralama (eşitlikte MatchId ile stabil).
/// </summary>
public interface IMatchRankingService
{
    Task<IReadOnlyList<RadarScore>> RankAsync(IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IMatchRankingService"/>
public sealed class MatchRankingService : IMatchRankingService
{
    private readonly IRadarInputBuilder _inputBuilder;
    private readonly IRadarScoreEngine _scoreEngine;
    private readonly RadarWeights _weights;

    public MatchRankingService(IRadarInputBuilder inputBuilder, IRadarScoreEngine scoreEngine, RadarWeights weights)
    {
        _inputBuilder = inputBuilder;
        _scoreEngine = scoreEngine;
        _weights = weights;
    }

    public async Task<IReadOnlyList<RadarScore>> RankAsync(IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        if (matchIds is null || matchIds.Count == 0)
            return System.Array.Empty<RadarScore>();

        var scores = new List<RadarScore>(matchIds.Count);
        foreach (var id in matchIds.Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = await _inputBuilder.BuildAsync(id, cancellationToken).ConfigureAwait(false);
            if (input is null) continue; // Feature Store'da yok → atla
            scores.Add(_scoreEngine.Score(input, _weights));
        }

        // Deterministik sıralama: yüksek skor önce, eşitlikte MatchId.
        return scores.OrderByDescending(s => s.Score).ThenBy(s => s.MatchId).ToList();
    }
}
