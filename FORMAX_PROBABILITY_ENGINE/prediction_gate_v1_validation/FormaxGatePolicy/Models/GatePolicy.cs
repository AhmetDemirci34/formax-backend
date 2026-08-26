using System.Globalization;
using Formax.Prediction.Config;

namespace Formax.GatePolicy.Models;

/// <summary>
/// One candidate gate policy. Only the two thresholds that actually decide ACCEPT/REJECT for this
/// dataset are swept; everything else in the gate is a correctness check, not a policy knob, and
/// stays on in every candidate.
/// </summary>
public sealed record Policy(int MinPriorMatches, int MinCompetitionMatches)
{
    /// <summary>
    /// The staleness net travels with the history bar. At MinPriorMatches = 0 it must also be 0,
    /// otherwise "publish everything" would still refuse a side whose decayed count sits under the
    /// net - which is the decay artefact this project already had to fix once.
    /// </summary>
    public double MinEffectiveMatches => MinPriorMatches == 0 ? 0.0 : 0.5;

    public string Name => $"P{MinPriorMatches}_C{MinCompetitionMatches}";

    public string Describe() =>
        $"minPriorMatchesPerTeam={MinPriorMatches}, minEffectiveMatchesPerTeam={MinEffectiveMatches.ToString(CultureInfo.InvariantCulture)}, " +
        $"minCompetitionMatchesObserved={MinCompetitionMatches}";

    /// <summary>Builds a gate config from a baseline, changing ONLY the swept thresholds.</summary>
    public GateConfig Apply(GateConfig baseline)
    {
        var c = new GateConfig
        {
            GateVersion = baseline.GateVersion + "/" + Name,
            ModelVersion = baseline.ModelVersion,
            TeamStrengthVersion = baseline.TeamStrengthVersion,
            RequiredIdentityConfidence = baseline.RequiredIdentityConfidence,
            MinPriorMatchesPerTeam = MinPriorMatches,
            MinEffectiveMatchesPerTeam = MinEffectiveMatches,
            MinCompetitionMatchesObserved = MinCompetitionMatches,
            LambdaMin = baseline.LambdaMin,
            LambdaMax = baseline.LambdaMax,
            GuardRailTolerance = baseline.GuardRailTolerance,
            ProbabilitySumTolerance = baseline.ProbabilitySumTolerance,
            ConfidenceLowMin = baseline.ConfidenceLowMin,
            ConfidenceMediumLowMin = baseline.ConfidenceMediumLowMin,
            ConfidenceMediumMin = baseline.ConfidenceMediumMin,
            ConfidenceHighMin = baseline.ConfidenceHighMin,
            MaxPriorWeightForFullConfidence = baseline.MaxPriorWeightForFullConfidence
        };
        c.Validate();
        return c;
    }
}

/// <summary>
/// The candidate set, written down in one place.
///
/// Six history bars and three coverage bars: eighteen policies. Deliberately small. A hundred
/// thresholds on 7.636 validation matches would find a winner by luck, and the winner would be
/// the noise rather than the policy.
/// </summary>
public static class PolicyGrid
{
    public static readonly int[] MinPriorMatches = { 0, 1, 2, 3, 5, 10 };
    public static readonly int[] MinCompetitionMatches = { 0, 50, 200 };

    /// <summary>The policy Prediction Gate V1 currently ships with. Everything is judged against this.</summary>
    public static readonly Policy Current = new(1, 50);

    /// <summary>The null policy: publish everything the correctness checks allow.</summary>
    public static readonly Policy NoGate = new(0, 0);

    public static IEnumerable<Policy> All()
    {
        foreach (var p in MinPriorMatches)
            foreach (var c in MinCompetitionMatches)
                yield return new Policy(p, c);
    }
}
