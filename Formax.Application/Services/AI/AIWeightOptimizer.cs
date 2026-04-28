using Formax.Application.Services.AI;

namespace Formax.Application.Services.AI;

public class AIWeightOptimizer
{
    private readonly TrainerMetricsCalculator _metrics;

    public AIWeightOptimizer(TrainerMetricsCalculator metrics)
    {
        _metrics = metrics;
    }

    public async Task<OptimizerResult> Optimize()
    {
        var ctr = await _metrics.CalculateCTR();
        var skipRate = await _metrics.CalculateSkipRate();
        var followRate = await _metrics.CalculateFollowRate();

        double explorationRate = 0.2;
        double rewardWeight = 1.0;
        double rankingBoost = 1.0;

        // CTR düşükse exploration artır
        if (ctr < 0.05)
            explorationRate = 0.35;

        if (ctr > 0.15)
            explorationRate = 0.15;

        // follow yüksekse reward artır
        if (followRate > 0.05)
            rewardWeight = 1.5;

        // skip yüksekse ranking azalt
        if (skipRate > 0.40)
            rankingBoost = 0.8;

        return new OptimizerResult
        {
            ExplorationRate = explorationRate,
            RewardWeight = rewardWeight,
            RankingBoost = rankingBoost
        };
    }
}

public class OptimizerResult
{
    public double ExplorationRate { get; set; }
    public double RewardWeight { get; set; }
    public double RankingBoost { get; set; }
}
