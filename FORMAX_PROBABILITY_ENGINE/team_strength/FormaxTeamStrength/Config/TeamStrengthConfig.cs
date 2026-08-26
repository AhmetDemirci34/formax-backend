using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.TeamStrength.Config;

/// <summary>
/// Every tunable lives here and is loaded from teamstrength.config.json.
/// NOTHING is hard-coded in the engine: the values below are defaults so the tool runs, and the
/// json file is the single place a value may be changed. None of them has been validated against
/// held-out data yet - selecting them is a later phase (walk-forward validation).
/// </summary>
public sealed class TeamStrengthConfig
{
    /// <summary>Half-life in days of the exponential time weight. UNVALIDATED default.</summary>
    public double HalfLifeDays { get; set; } = 365.0;

    /// <summary>Step size of the multiplicative update. UNVALIDATED default.</summary>
    public double LearningRate { get; set; } = 0.08;

    /// <summary>Shrinkage constant k in w = n_eff / (n_eff + k). UNVALIDATED default.</summary>
    public double ShrinkageK { get; set; } = 6.0;

    /// <summary>Additive smoothing used when forming the surprise ratio. UNVALIDATED default.</summary>
    public double RatioSmoothing { get; set; } = 0.60;

    /// <summary>Hard clamp so a single freak result cannot explode a rating.</summary>
    public double MinIndex { get; set; } = 0.25;
    public double MaxIndex { get; set; } = 4.00;

    /// <summary>Venue specific indices use their own shrinkage constant (fewer matches per venue).</summary>
    public double VenueShrinkageK { get; set; } = 4.0;

    /// <summary>Effective-match thresholds for the confidence / cold-start labels.</summary>
    public double LimitedThreshold { get; set; } = 1.0;     // >= this -> Limited
    public double DevelopingThreshold { get; set; } = 3.0;  // >= this -> Developing
    public double EstablishedThreshold { get; set; } = 5.0; // >= this -> Established
    public double RichThreshold { get; set; } = 10.0;       // >= this -> Rich

    /// <summary>
    /// Baseline goals per match are learned from the data with an expanding window, per
    /// CompetitionType. These seeds are only used until MinBaselineSamples matches of that type
    /// have been observed, and every snapshot records which prior it used.
    /// </summary>
    public double SeedBaselineHomeGoals { get; set; } = 1.50;
    public double SeedBaselineAwayGoals { get; set; } = 1.20;
    public int MinBaselineSamples { get; set; } = 50;

    /// <summary>Pool used as the shrinkage target when a team has little or no history.</summary>
    public bool UseCompetitionTypePool { get; set; } = true;

    /// <summary>Only rows with this identity confidence enter the engine.</summary>
    public string RequiredIdentityConfidence { get; set; } = "CONFIRMED";

    /// <summary>Match statuses accepted as played results.</summary>
    public string[] AcceptedMatchStatuses { get; set; } = new[] { "FT", "AET", "PEN" };

    [JsonIgnore]
    public string SourcePath { get; private set; } = "(defaults)";

    public static TeamStrengthConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"config not found: {path}");

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<TeamStrengthConfig>(json, new JsonSerializerOptions
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
        if (HalfLifeDays <= 0) throw new InvalidOperationException("HalfLifeDays must be > 0");
        if (LearningRate <= 0 || LearningRate > 1) throw new InvalidOperationException("LearningRate must be in (0,1]");
        if (ShrinkageK < 0) throw new InvalidOperationException("ShrinkageK must be >= 0");
        if (MinIndex <= 0 || MaxIndex <= MinIndex) throw new InvalidOperationException("index clamp is invalid");
        if (RichThreshold < EstablishedThreshold || EstablishedThreshold < DevelopingThreshold
            || DevelopingThreshold < LimitedThreshold)
            throw new InvalidOperationException("confidence thresholds must be non-decreasing");
    }

    public string Describe() => string.Join(" | ", new[]
    {
        $"halfLifeDays={HalfLifeDays.ToString(CultureInfo.InvariantCulture)}",
        $"learningRate={LearningRate.ToString(CultureInfo.InvariantCulture)}",
        $"shrinkageK={ShrinkageK.ToString(CultureInfo.InvariantCulture)}",
        $"venueShrinkageK={VenueShrinkageK.ToString(CultureInfo.InvariantCulture)}",
        $"ratioSmoothing={RatioSmoothing.ToString(CultureInfo.InvariantCulture)}",
        $"clamp=[{MinIndex.ToString(CultureInfo.InvariantCulture)},{MaxIndex.ToString(CultureInfo.InvariantCulture)}]",
        $"config={SourcePath}"
    });
}
