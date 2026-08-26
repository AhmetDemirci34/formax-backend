using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.Prediction.Config;

/// <summary>
/// The gate policy. Every threshold lives here; nothing is hard-coded in the gate.
///
/// IMPORTANT - these are POLICY, not fitted parameters. None of them was chosen by scoring the
/// test segment, and none of them can be: the gate decides whether to publish a probability, it
/// never changes one. Where a threshold is informed by measurement, the measurement comes from the
/// VALIDATION segment of the previous phase and is cited in the README.
/// </summary>
public sealed class GateConfig
{
    public string GateVersion { get; set; } = "PREDICTION_GATE_V1";
    public string ModelVersion { get; set; } = "INDEPENDENT_POISSON_V2";
    public string TeamStrengthVersion { get; set; } = "TEAM_STRENGTH_V2_VALIDATED";

    /// <summary>Rows whose team identity is not this are never predicted on.</summary>
    public string RequiredIdentityConfidence { get; set; } = "CONFIRMED";

    /// <summary>
    /// Minimum prior matches EACH side must have. 1 means: a side with zero history is refused.
    /// With zero prior matches the snapshot IS the prior, so the published number would describe
    /// the opponent and the competition, not this team.
    /// </summary>
    public int MinPriorMatchesPerTeam { get; set; } = 1;

    /// <summary>
    /// Staleness net: minimum time-DECAYED prior matches each side must have. Deliberately below
    /// <see cref="MinPriorMatchesPerTeam"/>. At 1.0 it fired on matches whose weaker side had
    /// exactly one prior match, purely because a 2% decay pushed the effective count under the
    /// integer bound - a rounding artefact wearing the costume of a policy. Refusing thin-history
    /// matches is a legitimate choice, but it belongs in MinPriorMatchesPerTeam where it is visible.
    /// </summary>
    public double MinEffectiveMatchesPerTeam { get; set; } = 0.5;

    /// <summary>
    /// Minimum matches of the competition type that must already have been observed, so its goal
    /// baseline is learned from data rather than taken from a config seed. Matches the frozen
    /// context's own MinBaselineSamples.
    /// </summary>
    public int MinCompetitionMatchesObserved { get; set; } = 50;

    /// <summary>Lambda clamp of the frozen model. A value sitting on either bound is saturated, not measured.</summary>
    public double LambdaMin { get; set; } = 0.05;
    public double LambdaMax { get; set; } = 6.0;
    public double GuardRailTolerance { get; set; } = 1e-9;

    /// <summary>How far the three probabilities may sum away from 1 before the prediction is refused.</summary>
    public double ProbabilitySumTolerance { get; set; } = 1e-12;

    /// <summary>Prior-match thresholds for the confidence classes. Evidence only - never the probability.</summary>
    public int ConfidenceLowMin { get; set; } = 1;
    public int ConfidenceMediumLowMin { get; set; } = 3;
    public int ConfidenceMediumMin { get; set; } = 5;
    public int ConfidenceHighMin { get; set; } = 10;

    /// <summary>
    /// Prior weight above which a side is treated as pooled rather than known, capping confidence.
    /// </summary>
    public double MaxPriorWeightForFullConfidence { get; set; } = 0.50;

    [JsonIgnore] public string SourcePath { get; private set; } = "(defaults)";

    public static GateConfig Load(string path)
    {
        GateConfig cfg;
        if (File.Exists(path))
        {
            cfg = JsonSerializer.Deserialize<GateConfig>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? throw new InvalidOperationException("gate config could not be parsed");
            cfg.SourcePath = path;
        }
        else cfg = new GateConfig();
        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        if (MinPriorMatchesPerTeam < 0) throw new InvalidOperationException("MinPriorMatchesPerTeam must be >= 0");
        if (LambdaMax <= LambdaMin) throw new InvalidOperationException("lambda clamp invalid");
        if (ProbabilitySumTolerance <= 0 || ProbabilitySumTolerance > 1e-6)
            throw new InvalidOperationException("probability sum tolerance invalid");
        if (ConfidenceHighMin < ConfidenceMediumMin || ConfidenceMediumMin < ConfidenceMediumLowMin
            || ConfidenceMediumLowMin < ConfidenceLowMin)
            throw new InvalidOperationException("confidence thresholds must be non-decreasing");
    }

    public string Describe() => string.Join(" | ", new[]
    {
        $"gate={GateVersion}",
        $"identity={RequiredIdentityConfidence}",
        $"minPriorMatches={MinPriorMatchesPerTeam.ToString(CultureInfo.InvariantCulture)}",
        $"minCompetitionMatches={MinCompetitionMatchesObserved.ToString(CultureInfo.InvariantCulture)}",
        $"confidence=[{ConfidenceLowMin}/{ConfidenceMediumLowMin}/{ConfidenceMediumMin}/{ConfidenceHighMin}]",
        $"config={SourcePath}"
    });
}
