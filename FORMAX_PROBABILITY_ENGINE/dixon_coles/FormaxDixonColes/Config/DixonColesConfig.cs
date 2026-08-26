using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.DixonColes.Config;

/// <summary>
/// Every tunable of the baseline probability model. NOTHING is hard-coded in the model code.
/// EVERY value below is UNVALIDATED: none of them has been fitted or selected on held-out data.
/// Choosing them is a later phase.
/// </summary>
public sealed class DixonColesConfig
{
    public string ConfigVersion { get; set; } = "DC_CONFIG_V1_UNVALIDATED";

    /// <summary>Dixon-Coles low-score dependence parameter. UNVALIDATED constant, not fitted.</summary>
    public double Rho { get; set; } = -0.05;

    /// <summary>Score grid upper bound per side. The grid is renormalised so probabilities sum to 1.</summary>
    public int MaxGoals { get; set; } = 10;

    /// <summary>Lambda clamp, so a broken strength value cannot produce an absurd expectation.</summary>
    public double MinLambda { get; set; } = 0.05;
    public double MaxLambda { get; set; } = 6.0;

    /// <summary>
    /// Goals-per-match baselines are learned per CompetitionType with an expanding window.
    /// The seeds are used only until MinBaselineSamples matches of that type have been seen.
    /// </summary>
    public double SeedBaselineHomeGoals { get; set; } = 1.50;
    public double SeedBaselineAwayGoals { get; set; } = 1.20;
    public int MinBaselineSamples { get; set; } = 50;

    /// <summary>Seed outcome frequencies for the naive baseline until enough matches are seen.</summary>
    public double SeedHomeRate { get; set; } = 0.44;
    public double SeedDrawRate { get; set; } = 0.25;
    public double SeedAwayRate { get; set; } = 0.31;
    public int MinFrequencySamples { get; set; } = 50;

    /// <summary>Floor applied to every reported probability before renormalisation (avoids log(0)).</summary>
    public double ProbabilityFloor { get; set; } = 1e-6;

    /// <summary>Team strength inputs are consumed as produced; these record which run was used.</summary>
    public string TeamStrengthVersion { get; set; } = "TEAM_STRENGTH_V1";
    public string ModelVersion { get; set; } = "DIXON_COLES_BASELINE_V1";

    [JsonIgnore] public string SourcePath { get; private set; } = "(defaults)";

    public static DixonColesConfig Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"config not found: {path}");
        var cfg = JsonSerializer.Deserialize<DixonColesConfig>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException("config could not be parsed");
        cfg.SourcePath = path;
        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        if (MaxGoals < 5) throw new InvalidOperationException("MaxGoals must be >= 5");
        if (MinLambda <= 0 || MaxLambda <= MinLambda) throw new InvalidOperationException("lambda clamp invalid");
        if (ProbabilityFloor <= 0 || ProbabilityFloor > 0.01) throw new InvalidOperationException("probability floor invalid");
        // Dixon-Coles tau stays positive only inside a bounded rho range for realistic lambdas.
        if (Rho < -0.9 || Rho > 0.9) throw new InvalidOperationException("Rho out of range");
    }

    public string Describe() => string.Join(" | ", new[]
    {
        $"rho={Rho.ToString(CultureInfo.InvariantCulture)} (UNVALIDATED)",
        $"maxGoals={MaxGoals}",
        $"lambdaClamp=[{MinLambda.ToString(CultureInfo.InvariantCulture)},{MaxLambda.ToString(CultureInfo.InvariantCulture)}]",
        $"configVersion={ConfigVersion}",
        $"config={SourcePath}"
    });
}
