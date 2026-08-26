using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

/// <summary>Bir kullanıcı (opsiyonel) + maç kümesi için Discovery Feed kuran servis sözleşmesi.</summary>
public interface IDiscoveryFeedService
{
    Task<DiscoveryFeed> BuildFeedAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovery Feed orkestrasyonu: her maç için Base Radar Score (FAZ 4.1) → Personal Radar Score (FAZ 4.2, userId
/// varsa) hesaplar, lig/takım/tarih/güç meta verisini Historical tablolarından çözerek <see cref="DiscoveryCandidate"/>
/// kurar ve <see cref="IDiscoveryEngine"/> ile çeşitli feed'i üretir. Radar Score'a DOKUNMAZ (yalnız kullanır).
/// userId null → Base skorla (kişiselleştirmesiz). Deterministik.
/// </summary>
public sealed class DiscoveryFeedService : IDiscoveryFeedService
{
    private const double EloMin = 1000, EloMax = 2200;

    private readonly IRadarInputBuilder _inputBuilder;
    private readonly IRadarScoreEngine _scoreEngine;
    private readonly IUserInterestSignalProvider _interestProvider;
    private readonly IPersonalRadarScoreEngine _personalEngine;
    private readonly IDiscoveryEngine _discoveryEngine;
    private readonly FormaxDbContext _db;
    private readonly RadarWeights _radarWeights;
    private readonly PersonalRadarWeights _personalWeights;
    private readonly DiscoveryWeights _discoveryWeights;

    public DiscoveryFeedService(
        IRadarInputBuilder inputBuilder,
        IRadarScoreEngine scoreEngine,
        IUserInterestSignalProvider interestProvider,
        IPersonalRadarScoreEngine personalEngine,
        IDiscoveryEngine discoveryEngine,
        FormaxDbContext db,
        RadarWeights radarWeights,
        PersonalRadarWeights personalWeights,
        DiscoveryWeights discoveryWeights)
    {
        _inputBuilder = inputBuilder;
        _scoreEngine = scoreEngine;
        _interestProvider = interestProvider;
        _personalEngine = personalEngine;
        _discoveryEngine = discoveryEngine;
        _db = db;
        _radarWeights = radarWeights;
        _personalWeights = personalWeights;
        _discoveryWeights = discoveryWeights;
    }

    public async Task<DiscoveryFeed> BuildFeedAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var candidates = new List<DiscoveryCandidate>();

        foreach (var id in matchIds.Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = await _inputBuilder.BuildAsync(id, cancellationToken).ConfigureAwait(false);
            if (input is null) continue;

            var baseScore = _scoreEngine.Score(input, _radarWeights); // Radar Score — değişmez

            double personalScore = baseScore.Score;
            var hiddenGem = baseScore.HiddenGem;
            if (userId is int uid)
            {
                var interest = await _interestProvider.GetAsync(uid, id, cancellationToken).ConfigureAwait(false);
                var personal = _personalEngine.Personalize(baseScore, interest, _personalWeights);
                personalScore = personal.PersonalScore;
                hiddenGem = personal.HiddenGem;
            }

            var descriptor = await _db.HistoricalMatches.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.HomeTeamId, x.AwayTeamId, x.HistoricalCompetitionId, x.MatchDate })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (descriptor is null) continue;

            var league = await _db.HistoricalCompetitions.AsNoTracking()
                .Where(c => c.Id == descriptor.HistoricalCompetitionId).Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? "Unknown";

            var homeElo = input.Features.Home.Elo ?? 0;
            var awayElo = input.Features.Away.Elo ?? 0;
            var strength = Math.Clamp(((homeElo + awayElo) / 2.0 - EloMin) / (EloMax - EloMin), 0, 1);

            candidates.Add(new DiscoveryCandidate
            {
                MatchId = id,
                PersonalScore = personalScore,
                BaseScore = baseScore.Score,
                HiddenGem = hiddenGem,
                League = league,
                HomeTeamId = descriptor.HomeTeamId,
                AwayTeamId = descriptor.AwayTeamId,
                TeamStrength = strength,
                MatchDate = descriptor.MatchDate,
                Confidence = Math.Clamp(input.Confidence.ConfidenceScore, 0, 1)
            });
        }

        return _discoveryEngine.BuildFeed(candidates, _discoveryWeights);
    }
}
