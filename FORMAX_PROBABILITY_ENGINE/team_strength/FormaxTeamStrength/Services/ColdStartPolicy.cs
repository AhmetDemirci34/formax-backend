using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;

namespace Formax.TeamStrength.Services;

/// <summary>
/// The cold-start model, isolated so it can be reviewed on its own.
///
/// Four situations, exactly as specified:
///   A rich history      -> own evidence dominates, prior weight tends to 0
///   B limited history   -> partial pooling, prior weight = k / (n_eff + k)
///   C new team          -> prior + whatever evidence exists
///   D no history at all  -> the snapshot IS the prior; PriorSource says which pool it came from
///
/// A pool is only used once it has been observed enough times (MinBaselineSamples); until then the
/// neutral index 1.0 is used and the snapshot records GLOBAL_NEUTRAL. Nothing is invented: the
/// prior is always either a learned pool mean or the explicit neutral value.
/// </summary>
public static class ColdStartPolicy
{
    public readonly record struct Prior(double Attack, double Defense, string Source, bool PoolUsed);

    public static Prior ResolvePrior(
        TeamStrengthConfig cfg,
        string competitionType,
        bool poolReady,
        double poolAttack,
        double poolDefense,
        int poolObservations,
        int teamMatches)
    {
        var usePool = cfg.UseCompetitionTypePool && poolReady;
        if (!usePool)
            return new Prior(1.0, 1.0, "GLOBAL_NEUTRAL", false);

        var src = teamMatches == 0
            ? $"POOL:{competitionType}(n={poolObservations})"
            : $"POOL:{competitionType}";
        return new Prior(poolAttack, poolDefense, src, true);
    }

    /// <summary>Weight given to the team's own evidence: n_eff / (n_eff + k).</summary>
    public static double OwnEvidenceWeight(double effectiveMatches, double k)
        => effectiveMatches <= 0 ? 0.0 : effectiveMatches / (effectiveMatches + k);

    /// <summary>
    /// Two different questions, deliberately answered by two different numbers:
    ///   ColdStartClass  = how many prior matches exist at all      -> RAW count
    ///   ConfidenceLevel = how much weight that evidence carries    -> TIME-DECAYED count
    /// A club with 20 matches played five years ago is still "Rich" in history but its confidence
    /// drops, which is exactly what the decay is for.
    /// </summary>
    public static (ColdStartClass cls, ConfidenceLevel confidence) Classify(
        TeamStrengthConfig cfg, int rawMatches, double effectiveMatches)
    {
        ColdStartClass cls;
        if (rawMatches < cfg.LimitedThreshold) cls = ColdStartClass.NoHistory;
        else if (rawMatches < cfg.DevelopingThreshold) cls = ColdStartClass.Limited;
        else if (rawMatches < cfg.EstablishedThreshold) cls = ColdStartClass.Developing;
        else if (rawMatches < cfg.RichThreshold) cls = ColdStartClass.Established;
        else cls = ColdStartClass.Rich;

        ConfidenceLevel conf;
        if (effectiveMatches < cfg.LimitedThreshold) conf = ConfidenceLevel.None;
        else if (effectiveMatches < cfg.DevelopingThreshold) conf = ConfidenceLevel.Low;
        else if (effectiveMatches < cfg.EstablishedThreshold) conf = ConfidenceLevel.MediumLow;
        else if (effectiveMatches < cfg.RichThreshold) conf = ConfidenceLevel.Medium;
        else conf = ConfidenceLevel.High;

        return (cls, conf);
    }

    /// <summary>Blend own evidence with the prior.</summary>
    public static double Blend(double rawIndex, double priorIndex, double ownWeight)
        => priorIndex + (rawIndex - priorIndex) * ownWeight;
}
