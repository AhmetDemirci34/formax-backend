using Formax.Prediction.Config;
using Formax.Prediction.Models;

namespace Formax.Prediction.Services;

/// <summary>Only the evidence. Note what is NOT here: no probability, no lambda, no favourite, no margin.</summary>
public readonly struct ConfidenceInput
{
    public ConfidenceInput(int homePriorMatches, int awayPriorMatches,
        double homeEffectiveMatches, double awayEffectiveMatches,
        double homePriorWeight, double awayPriorWeight, int competitionMatchesObserved)
    {
        HomePriorMatches = homePriorMatches;
        AwayPriorMatches = awayPriorMatches;
        HomeEffectiveMatches = homeEffectiveMatches;
        AwayEffectiveMatches = awayEffectiveMatches;
        HomePriorWeight = homePriorWeight;
        AwayPriorWeight = awayPriorWeight;
        CompetitionMatchesObserved = competitionMatchesObserved;
    }

    public int HomePriorMatches { get; }
    public int AwayPriorMatches { get; }
    public double HomeEffectiveMatches { get; }
    public double AwayEffectiveMatches { get; }
    public double HomePriorWeight { get; }
    public double AwayPriorWeight { get; }
    public int CompetitionMatchesObserved { get; }
}

/// <summary>
/// Confidence is a statement about the EVIDENCE, never about the probability.
///
/// The signature makes this structural rather than a promise: <see cref="Classify"/> takes a
/// <see cref="ConfidenceInput"/>, which has no field carrying a probability, a lambda or an
/// outcome. A future edit cannot quietly start reading "the model is 80% sure so confidence is
/// HIGH" without changing the signature, which a reviewer would see.
///
/// The class is set by the WEAKER side. A match between a club with 400 matches of history and a
/// debutant is not well evidenced; it is half well evidenced, and the half that is missing is the
/// half that decides whether the number can be trusted.
/// </summary>
public sealed class ConfidenceClassifier
{
    private readonly GateConfig _cfg;
    public ConfidenceClassifier(GateConfig cfg) => _cfg = cfg;

    public ConfidenceClass Classify(ConfidenceInput e)
    {
        var weakestRaw = Math.Min(e.HomePriorMatches, e.AwayPriorMatches);
        var weakestEffective = Math.Min(e.HomeEffectiveMatches, e.AwayEffectiveMatches);
        var heaviestPrior = Math.Max(e.HomePriorWeight, e.AwayPriorWeight);

        // raw count sets the ceiling: how much evidence exists at all
        var cls =
            weakestRaw >= _cfg.ConfidenceHighMin ? ConfidenceClass.High
          : weakestRaw >= _cfg.ConfidenceMediumMin ? ConfidenceClass.Medium
          : weakestRaw >= _cfg.ConfidenceMediumLowMin ? ConfidenceClass.MediumLow
          : weakestRaw >= _cfg.ConfidenceLowMin ? ConfidenceClass.Low
          : ConfidenceClass.None;

        // time-decayed count can only lower it: old evidence is less evidence
        var decayed =
            weakestEffective >= _cfg.ConfidenceHighMin ? ConfidenceClass.High
          : weakestEffective >= _cfg.ConfidenceMediumMin ? ConfidenceClass.Medium
          : weakestEffective >= _cfg.ConfidenceMediumLowMin ? ConfidenceClass.MediumLow
          : weakestEffective >= _cfg.ConfidenceLowMin ? ConfidenceClass.Low
          : ConfidenceClass.None;
        if (decayed < cls) cls = decayed;

        // NONE means "no evidence at all". A side that HAS played is at worst LOW, however heavily
        // its evidence has decayed - "one old match" is thin evidence, not absent evidence. Without
        // this floor the decayed ladder reads NONE for a team with exactly one prior match whose
        // weight has decayed by 2%, which would publish a probability while declaring there is no
        // evidence behind it. The two statements cannot both be true.
        if (weakestRaw >= _cfg.ConfidenceLowMin && cls == ConfidenceClass.None) cls = ConfidenceClass.Low;

        // a side leaning mostly on a pool prior is not a known side, whatever its match count says
        if (heaviestPrior > _cfg.MaxPriorWeightForFullConfidence && cls > ConfidenceClass.Low)
            cls = ConfidenceClass.Low;

        // a competition whose own baseline is still a config seed caps the whole thing
        if (e.CompetitionMatchesObserved < _cfg.MinCompetitionMatchesObserved && cls > ConfidenceClass.Low)
            cls = ConfidenceClass.Low;

        return cls;
    }
}
