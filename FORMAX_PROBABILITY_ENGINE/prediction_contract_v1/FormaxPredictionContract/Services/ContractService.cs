using System.Security.Cryptography;
using Formax.Contract.Models;
using Formax.ModelValidation.Models;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Models;

namespace Formax.Contract.Services;

/// <summary>
/// Turns one raw model output into one canonical <see cref="PredictionContract"/>.
///
/// WHAT THIS LAYER MAY DO: decide whether to publish, classify the evidence, stamp versions and
/// identity.
///
/// WHAT IT MAY NOT DO, and structurally cannot: change a probability. The number the model produced
/// is copied into the contract unchanged when the gate accepts, and withheld entirely when it does
/// not. There is no rounding here, no clipping, no floor, no formatting - and no LLM: this class
/// has no dependency that could reach one.
/// </summary>
public sealed class ContractService
{
    private readonly PredictionService _gateService;
    private readonly ContractVersions _versions;

    /// <summary>SHA-256 of the validated team strength config file, taken at start-up.</summary>
    public string ModelFingerprint { get; }

    public ContractService(GateConfig gateConfig, ContractVersions versions, string validatedConfigPath)
    {
        _gateService = new PredictionService(gateConfig);
        _versions = versions;
        ModelFingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(validatedConfigPath)))[..32];
    }

    public PredictionContract Build(
        MatchPrediction raw,
        TeamStrengthSnapshot? home,
        TeamStrengthSnapshot? away,
        string homeIdentityConfidence,
        string awayIdentityConfidence,
        int competitionMatchesObserved)
    {
        var dto = _gateService.Build(raw, home, away, homeIdentityConfidence, awayIdentityConfidence,
            competitionMatchesObserved);

        // In replay the prediction is made from state that predates the match day, so the honest
        // timestamp is the start of that day in UTC. In live operation this is the wall clock; the
        // replay value is fixed so the whole stream is reproducible byte for byte.
        var timestamp = raw.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return new PredictionContract
        {
            PredictionId = PredictionIdFactory.Create(raw.MatchId, _versions, raw.EvidenceCutoff, timestamp),
            MatchId = raw.MatchId,
            MatchDate = raw.Date,
            PredictionTimestamp = timestamp,
            EvidenceCutoff = raw.EvidenceCutoff,
            Versions = _versions,

            HomeProbability = dto.HomeProbability,
            DrawProbability = dto.DrawProbability,
            AwayProbability = dto.AwayProbability,

            PredictionEligible = dto.PredictionEligible,
            ConfidenceClass = Map(dto.ConfidenceClass),
            GateStatus = dto.PredictionEligible ? GateStatus.Accepted : GateStatus.Rejected,
            GateReason = dto.GateReason
        };
    }

    private static ConfidenceClass Map(Formax.Prediction.Models.ConfidenceClass c) => c switch
    {
        Formax.Prediction.Models.ConfidenceClass.None => ConfidenceClass.None,
        Formax.Prediction.Models.ConfidenceClass.Low => ConfidenceClass.Low,
        Formax.Prediction.Models.ConfidenceClass.MediumLow => ConfidenceClass.MediumLow,
        Formax.Prediction.Models.ConfidenceClass.Medium => ConfidenceClass.Medium,
        _ => ConfidenceClass.High
    };
}
