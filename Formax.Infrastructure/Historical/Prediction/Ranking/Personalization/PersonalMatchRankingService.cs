using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>
/// Kişisel Radar sıralaması: "bu kullanıcı bugün hangi maçı izlemeli?" Base ranking'i (FAZ 4.1) DEĞİŞTİRMEDEN
/// yeniden kullanır, üstüne User Interest katmanını uygular ve PersonalScore'a göre sıralar.
/// </summary>
public interface IPersonalMatchRankingService
{
    Task<IReadOnlyList<PersonalRadarScore>> RankForUserAsync(int userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IPersonalMatchRankingService"/>
public sealed class PersonalMatchRankingService : IPersonalMatchRankingService
{
    private readonly IRadarInputBuilder _inputBuilder;
    private readonly IRadarScoreEngine _scoreEngine;
    private readonly IUserInterestSignalProvider _interestProvider;
    private readonly IPersonalRadarScoreEngine _personalEngine;
    private readonly RadarWeights _radarWeights;
    private readonly PersonalRadarWeights _personalWeights;

    public PersonalMatchRankingService(
        IRadarInputBuilder inputBuilder,
        IRadarScoreEngine scoreEngine,
        IUserInterestSignalProvider interestProvider,
        IPersonalRadarScoreEngine personalEngine,
        RadarWeights radarWeights,
        PersonalRadarWeights personalWeights)
    {
        _inputBuilder = inputBuilder;
        _scoreEngine = scoreEngine;
        _interestProvider = interestProvider;
        _personalEngine = personalEngine;
        _radarWeights = radarWeights;
        _personalWeights = personalWeights;
    }

    public async Task<IReadOnlyList<PersonalRadarScore>> RankForUserAsync(int userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        if (matchIds is null || matchIds.Count == 0)
            return System.Array.Empty<PersonalRadarScore>();

        var scores = new List<PersonalRadarScore>(matchIds.Count);
        foreach (var id in matchIds.Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = await _inputBuilder.BuildAsync(id, cancellationToken).ConfigureAwait(false);
            if (input is null) continue;

            var baseScore = _scoreEngine.Score(input, _radarWeights);                       // Base Radar Score — değişmez
            var interest = await _interestProvider.GetAsync(userId, id, cancellationToken).ConfigureAwait(false);
            scores.Add(_personalEngine.Personalize(baseScore, interest, _personalWeights));  // Personal = Base + Interest
        }

        return scores.OrderByDescending(s => s.PersonalScore).ThenBy(s => s.MatchId).ToList();
    }
}
