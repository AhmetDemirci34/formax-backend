using Formax.DixonColes.Models;
using Formax.ModelValidation.Models;
using Formax.Prediction.Config;
using Formax.Prediction.Models;
using Formax.TeamStrength.Models;

namespace Formax.Prediction.Services;

/// <summary>
/// Turns one raw model output into one published prediction.
///
/// The contract of this class, and the thing the regression check exists to prove: it NEVER changes
/// a probability. The number the model produced is copied into the DTO byte for byte when the gate
/// passes, and withheld entirely when it does not. There is no rounding, no clipping, no smoothing
/// and no floor applied here - the model's own floor and lambda clamp are part of the frozen model
/// and were not touched.
/// </summary>
public sealed class PredictionService
{
    private readonly GateConfig _cfg;
    private readonly PredictionGate _gate;
    private readonly ConfidenceClassifier _confidence;

    public PredictionService(GateConfig cfg)
    {
        _cfg = cfg;
        _gate = new PredictionGate(cfg);
        _confidence = new ConfidenceClassifier(cfg);
    }

    public PredictionDto Build(
        MatchPrediction raw,
        TeamStrengthSnapshot? home,
        TeamStrengthSnapshot? away,
        string homeIdentityConfidence,
        string awayIdentityConfidence,
        int competitionMatchesObserved)
    {
        var p = raw.Probabilities[(int)ModelId.IndependentPoisson];

        var input = new GateInput
        {
            HomeSnapshotPresent = home is not null,
            AwaySnapshotPresent = away is not null,
            HomeIdentityConfidence = homeIdentityConfidence,
            AwayIdentityConfidence = awayIdentityConfidence,
            HomePriorMatches = home?.MatchesUsed ?? 0,
            AwayPriorMatches = away?.MatchesUsed ?? 0,
            HomeEffectiveMatches = home?.EffectiveMatches ?? 0,
            AwayEffectiveMatches = away?.EffectiveMatches ?? 0,
            HomePriorWeight = home?.PriorWeight ?? 1.0,
            AwayPriorWeight = away?.PriorWeight ?? 1.0,
            CompetitionMatchesObserved = competitionMatchesObserved,
            LambdaHome = raw.LambdaHome,
            LambdaAway = raw.LambdaAway,
            ProbHome = p.Home,
            ProbDraw = p.Draw,
            ProbAway = p.Away,
            MatchDate = raw.Date,
            EvidenceCutoff = raw.EvidenceCutoff
        };

        var gate = _gate.Evaluate(input);

        var confidence = _confidence.Classify(new ConfidenceInput(
            input.HomePriorMatches, input.AwayPriorMatches,
            input.HomeEffectiveMatches, input.AwayEffectiveMatches,
            input.HomePriorWeight, input.AwayPriorWeight,
            competitionMatchesObserved));

        return new PredictionDto
        {
            MatchId = raw.MatchId,
            MatchDate = raw.Date,

            // published only when the gate passed - and then exactly as the model produced them
            HomeProbability = gate.Eligible ? p.Home : null,
            DrawProbability = gate.Eligible ? p.Draw : null,
            AwayProbability = gate.Eligible ? p.Away : null,

            ModelVersion = _cfg.ModelVersion,
            TeamStrengthVersion = _cfg.TeamStrengthVersion,
            PredictionTimestamp = raw.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            EvidenceCutoff = raw.EvidenceCutoff,

            ConfidenceClass = confidence,
            PredictionEligible = gate.Eligible,
            GateReason = gate.Reason,

            ModelHomeProbability = p.Home,
            ModelDrawProbability = p.Draw,
            ModelAwayProbability = p.Away,
            LambdaHome = raw.LambdaHome,
            LambdaAway = raw.LambdaAway,
            CompetitionType = raw.CompetitionType,
            Competition = raw.Competition,
            HomePriorMatches = input.HomePriorMatches,
            AwayPriorMatches = input.AwayPriorMatches,
            HomeColdStartClass = home?.ColdStartClass.ToString() ?? "",
            AwayColdStartClass = away?.ColdStartClass.ToString() ?? "",
            CompetitionMatchesObserved = competitionMatchesObserved
        };
    }
}
