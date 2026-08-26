using Formax.Prediction.Config;
using Formax.Prediction.Models;

namespace Formax.Prediction.Services;

/// <summary>Everything the gate is allowed to look at. Deliberately not a match object: the gate inspects EVIDENCE, not football.</summary>
public sealed record GateInput
{
    public required bool HomeSnapshotPresent { get; init; }
    public required bool AwaySnapshotPresent { get; init; }
    public required string HomeIdentityConfidence { get; init; }
    public required string AwayIdentityConfidence { get; init; }

    public required int HomePriorMatches { get; init; }
    public required int AwayPriorMatches { get; init; }
    public required double HomeEffectiveMatches { get; init; }
    public required double AwayEffectiveMatches { get; init; }
    public required double HomePriorWeight { get; init; }
    public required double AwayPriorWeight { get; init; }

    public required int CompetitionMatchesObserved { get; init; }

    public required double LambdaHome { get; init; }
    public required double LambdaAway { get; init; }
    public required double ProbHome { get; init; }
    public required double ProbDraw { get; init; }
    public required double ProbAway { get; init; }

    public required DateOnly MatchDate { get; init; }
    public required DateOnly? EvidenceCutoff { get; init; }
}

public sealed class GateResult
{
    public required bool Eligible { get; init; }
    public required IReadOnlyList<GateCode> Codes { get; init; }
    public string Reason => Eligible ? "OK" : string.Join('|', Codes.Select(c => Code(c)));

    public static string Code(GateCode c) => c switch
    {
        GateCode.IdentityUnconfirmed => "IDENTITY_UNCONFIRMED",
        GateCode.SnapshotMissing => "SNAPSHOT_MISSING",
        GateCode.NoTeamHistory => "NO_TEAM_HISTORY",
        GateCode.InsufficientHistory => "INSUFFICIENT_HISTORY",
        GateCode.CompetitionNotCovered => "COMPETITION_NOT_COVERED",
        GateCode.ModelStateAtGuardRail => "MODEL_STATE_AT_GUARD_RAIL",
        GateCode.ModelStateInvalid => "MODEL_STATE_INVALID",
        GateCode.ProbabilityNotNormalised => "PROBABILITY_NOT_NORMALISED",
        _ => "EVIDENCE_NOT_STRICTLY_PRE_MATCH"
    };
}

/// <summary>
/// The prediction gate.
///
/// It answers one question - may this probability be published? - and it answers it WITHOUT
/// touching the probability. There is no clipping, no flooring, no "90% is too high, make it 85".
/// A probability either goes out exactly as the model produced it, or it does not go out.
///
/// Every check is about evidence or state validity:
///   identity      - do we know who is playing
///   history       - does each side have any of its own evidence
///   coverage      - has this competition been seen enough for its baseline to be learned
///   model state   - are the lambdas finite and off their clamps
///   normalisation - do the three probabilities form a distribution
///   time          - is every piece of evidence strictly older than the match
///
/// All failing checks are collected, not short-circuited: an operator should see every reason at
/// once rather than fixing them one release at a time.
/// </summary>
public sealed class PredictionGate
{
    private readonly GateConfig _cfg;
    public PredictionGate(GateConfig cfg) => _cfg = cfg;

    public GateResult Evaluate(GateInput g)
    {
        var codes = new List<GateCode>();

        // ---- required state exists at all
        if (!g.HomeSnapshotPresent || !g.AwaySnapshotPresent)
            codes.Add(GateCode.SnapshotMissing);

        // ---- identity
        if (!string.Equals(g.HomeIdentityConfidence, _cfg.RequiredIdentityConfidence, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(g.AwayIdentityConfidence, _cfg.RequiredIdentityConfidence, StringComparison.OrdinalIgnoreCase))
            codes.Add(GateCode.IdentityUnconfirmed);

        // ---- history. Zero and "some but not enough" are different failures and are reported as such.
        //
        // The zero case is gated on the policy rather than hard-coded, so that a policy of
        // minPriorMatchesPerTeam = 0 genuinely means "publish everything, however thin". Without
        // that, the policy sweep could not express its own null hypothesis - and a threshold you
        // cannot turn off is not a threshold, it is a belief.
        if (_cfg.MinPriorMatchesPerTeam >= 1 && (g.HomePriorMatches <= 0 || g.AwayPriorMatches <= 0))
            codes.Add(GateCode.NoTeamHistory);
        else if (g.HomePriorMatches < _cfg.MinPriorMatchesPerTeam || g.AwayPriorMatches < _cfg.MinPriorMatchesPerTeam
              || g.HomeEffectiveMatches < _cfg.MinEffectiveMatchesPerTeam
              || g.AwayEffectiveMatches < _cfg.MinEffectiveMatchesPerTeam)
            codes.Add(GateCode.InsufficientHistory);

        // ---- competition coverage
        if (g.CompetitionMatchesObserved < _cfg.MinCompetitionMatchesObserved)
            codes.Add(GateCode.CompetitionNotCovered);

        // ---- model state
        if (!Finite(g.LambdaHome) || !Finite(g.LambdaAway) || g.LambdaHome <= 0 || g.LambdaAway <= 0 ||
            !Finite(g.ProbHome) || !Finite(g.ProbDraw) || !Finite(g.ProbAway) ||
            g.ProbHome < 0 || g.ProbDraw < 0 || g.ProbAway < 0 ||
            g.ProbHome > 1 || g.ProbDraw > 1 || g.ProbAway > 1)
            codes.Add(GateCode.ModelStateInvalid);
        else if (AtRail(g.LambdaHome) || AtRail(g.LambdaAway))
            codes.Add(GateCode.ModelStateAtGuardRail);

        // ---- normalisation
        if (Math.Abs(g.ProbHome + g.ProbDraw + g.ProbAway - 1.0) > _cfg.ProbabilitySumTolerance)
            codes.Add(GateCode.ProbabilityNotNormalised);

        // ---- time: evidence must be strictly older than the match it predicts
        if (g.EvidenceCutoff.HasValue && g.EvidenceCutoff.Value >= g.MatchDate)
            codes.Add(GateCode.EvidenceNotStrictlyPreMatch);

        return new GateResult { Eligible = codes.Count == 0, Codes = codes };
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private bool AtRail(double lambda)
        => lambda <= _cfg.LambdaMin + _cfg.GuardRailTolerance
        || lambda >= _cfg.LambdaMax - _cfg.GuardRailTolerance;
}
