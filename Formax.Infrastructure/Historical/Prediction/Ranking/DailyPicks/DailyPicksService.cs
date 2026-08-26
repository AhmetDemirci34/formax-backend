using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Ranking.HiddenGems;
using Formax.Infrastructure.Historical.Prediction.Ranking.Recommendation;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.DailyPicks;

/// <summary>Günün en iyi maç listesini üreten servis sözleşmesi.</summary>
public interface IDailyPicksService
{
    Task<DailyPicksResult> GetDailyPicksAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Daily Picks — Discovery Feed'i (sırasını KORUYARAK) + Recommendation + Hidden Gems çıktılarını okuyup günün
/// seçimlerini üretir: Today's Best (feed #1), Top 5 (feed ilk 5), Best Hidden Gem, Best High Confidence, Best
/// Goal, Best Tight. Mevcut motorlara DOKUNMAZ; yalnız SEÇER (argmax, deterministik tie-break MatchId).
/// Config-driven (alt katman ağırlıkları). Deterministik.
/// </summary>
public sealed class DailyPicksService : IDailyPicksService
{
    private readonly IRadarContextBuilder _contextBuilder;
    private readonly IMatchRecommendationEngine _recommendationEngine;
    private readonly IHiddenGemsEngine _hiddenGemsEngine;
    private readonly RecommendationWeights _recommendationWeights;
    private readonly HiddenGemWeights _hiddenGemWeights;

    public DailyPicksService(
        IRadarContextBuilder contextBuilder,
        IMatchRecommendationEngine recommendationEngine,
        IHiddenGemsEngine hiddenGemsEngine,
        RecommendationWeights recommendationWeights,
        HiddenGemWeights hiddenGemWeights)
    {
        _contextBuilder = contextBuilder;
        _recommendationEngine = recommendationEngine;
        _hiddenGemsEngine = hiddenGemsEngine;
        _recommendationWeights = recommendationWeights;
        _hiddenGemWeights = hiddenGemWeights;
    }

    public async Task<DailyPicksResult> GetDailyPicksAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var contexts = await _contextBuilder.BuildContextsAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);
        if (contexts.Count == 0)
            return new DailyPicksResult { Top5 = Array.Empty<DailyPick>() };

        // Her maç için öneri + gem (mevcut motorlar, değiştirilmeden).
        var rows = contexts.Select(c =>
        {
            var rec = _recommendationEngine.Recommend(new RecommendationInput
            {
                MatchId = c.MatchId, Discovery = c.Discovery, Radar = c.Radar, Probability = c.Probability, Confidence = c.Confidence
            }, _recommendationWeights);
            var gem = _hiddenGemsEngine.Analyze(c, _hiddenGemWeights);
            return (ctx: c, rec, gem);
        }).ToList();

        DailyPick Pick(RadarMatchContext c, MatchRecommendation rec, string category, double score)
            => new()
            {
                MatchId = c.MatchId, Category = category, Score = Math.Round(score, 4),
                Reason = rec.Reasons.Count > 0 ? rec.Reasons[0] : c.Discovery.DiscoveryReason,
                Tags = rec.Tags, HiddenGem = rec.HiddenGem,
                DiscoveryScore = c.Discovery.DiscoveryScore, PersonalScore = c.Discovery.PersonalScore, RecommendationScore = rec.RecommendationScore
            };

        double SigVal(RadarMatchContext c, string name)
        {
            var s = c.Radar.Breakdown.FirstOrDefault(b => b.Signal == name);
            return s is { DataAvailable: true } ? s.Value : double.NegativeInfinity;
        }

        // Today's Best = Discovery feed #1 (sıralama korunur).
        var best = rows[0];
        var todaysBest = Pick(best.ctx, best.rec, "TodaysBest", best.ctx.Discovery.DiscoveryScore);

        var top5 = rows.Take(5).Select(r => Pick(r.ctx, r.rec, "Top5", r.ctx.Discovery.DiscoveryScore)).ToList();

        // Best Hidden Gem (gerçekten gem olanlar arasında en yüksek HiddenGemScore).
        var gemRow = rows.Where(r => r.gem.IsHiddenGem).OrderByDescending(r => r.gem.HiddenGemScore).ThenBy(r => r.ctx.MatchId).FirstOrDefault();
        var bestGem = gemRow.ctx is null ? null : Pick(gemRow.ctx, gemRow.rec, "HiddenGem", gemRow.gem.HiddenGemScore);

        // Best High Confidence / Goal / Tight — argmax (deterministik tie-break MatchId).
        var bestConf = ArgMax(rows, r => r.ctx.Confidence.ConfidenceScore);
        var bestGoal = ArgMax(rows, r => SigVal(r.ctx, "GoalPotential"));
        var bestTight = ArgMax(rows, r => SigVal(r.ctx, "Competitiveness"));

        return new DailyPicksResult
        {
            TodaysBestMatch = todaysBest,
            Top5 = top5,
            BestHiddenGem = bestGem,
            BestHighConfidenceMatch = bestConf.ctx is null ? null : Pick(bestConf.ctx, bestConf.rec, "HighConfidence", bestConf.ctx.Confidence.ConfidenceScore),
            BestGoalMatch = bestGoal.ctx is null ? null : Pick(bestGoal.ctx, bestGoal.rec, "GoalMatch", SigVal(bestGoal.ctx, "GoalPotential")),
            BestTightMatch = bestTight.ctx is null ? null : Pick(bestTight.ctx, bestTight.rec, "TightMatch", SigVal(bestTight.ctx, "Competitiveness"))
        };
    }

    private static (RadarMatchContext ctx, MatchRecommendation rec, HiddenGemAnalysis gem) ArgMax(
        List<(RadarMatchContext ctx, MatchRecommendation rec, HiddenGemAnalysis gem)> rows, Func<(RadarMatchContext ctx, MatchRecommendation rec, HiddenGemAnalysis gem), double> selector)
    {
        (RadarMatchContext ctx, MatchRecommendation rec, HiddenGemAnalysis gem) best = default;
        double bestVal = double.NegativeInfinity;
        foreach (var r in rows)
        {
            var v = selector(r);
            if (v == double.NegativeInfinity) continue;
            if (best.ctx is null || v > bestVal || (v == bestVal && r.ctx.MatchId < best.ctx.MatchId))
            {
                best = r; bestVal = v;
            }
        }
        return best;
    }
}
