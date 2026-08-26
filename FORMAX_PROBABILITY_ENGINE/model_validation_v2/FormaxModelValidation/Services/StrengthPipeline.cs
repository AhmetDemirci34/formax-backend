using Formax.DixonColes.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;

namespace Formax.ModelValidation.Services;

/// <summary>
/// Runs the unmodified team strength engine in memory for one candidate config and hands the
/// resulting pre-match snapshots to the backtest in exactly the shape the V1 backtest consumed
/// them from CSV.
///
/// In memory, not through the CSV, for one reason: a grid search writing 68k rows per candidate
/// would be pointless I/O. The one difference this makes is precision - the CSV rounds every index
/// to six decimals - and the "csv vs memory" test measures that difference instead of assuming it
/// away.
/// </summary>
public static class StrengthPipeline
{
    public static Dictionary<(string matchId, string side), StrengthRow> BuildRows(
        TeamStrengthConfig cfg, IEnumerable<MatchRecord> matches)
    {
        var built = new TeamStrengthService(cfg).Build(matches);
        var map = new Dictionary<(string, string), StrengthRow>(built.Snapshots.Count);
        foreach (var s in built.Snapshots) map[(s.MatchId, s.Side)] = ToRow(s);
        return map;
    }

    public static StrengthRow ToRow(TeamStrengthSnapshot s) => new()
    {
        MatchId = s.MatchId,
        TeamId = s.TeamId,
        Side = s.Side,
        MatchDate = s.MatchDate,
        Attack = s.AttackStrength,
        Defense = s.DefenseStrength,
        Overall = s.OverallStrength,
        HomeStrength = s.HomeStrength,
        AwayStrength = s.AwayStrength,
        MatchesUsed = s.MatchesUsed,
        Confidence = s.Confidence.ToString(),
        ColdStartClass = s.ColdStartClass.ToString(),
        PriorSource = s.PriorSource,
        PriorWeight = s.PriorWeight,
        LastMatchDate = s.LastMatchDate
    };

    /// <summary>A copy of the config with every field carried over. Candidates are built by mutating a copy.</summary>
    public static TeamStrengthConfig Copy(TeamStrengthConfig c) => new()
    {
        HalfLifeDays = c.HalfLifeDays,
        LearningRate = c.LearningRate,
        ShrinkageK = c.ShrinkageK,
        RatioSmoothing = c.RatioSmoothing,
        MinIndex = c.MinIndex,
        MaxIndex = c.MaxIndex,
        VenueShrinkageK = c.VenueShrinkageK,
        LimitedThreshold = c.LimitedThreshold,
        DevelopingThreshold = c.DevelopingThreshold,
        EstablishedThreshold = c.EstablishedThreshold,
        RichThreshold = c.RichThreshold,
        SeedBaselineHomeGoals = c.SeedBaselineHomeGoals,
        SeedBaselineAwayGoals = c.SeedBaselineAwayGoals,
        MinBaselineSamples = c.MinBaselineSamples,
        UseCompetitionTypePool = c.UseCompetitionTypePool,
        RequiredIdentityConfidence = c.RequiredIdentityConfidence,
        AcceptedMatchStatuses = (string[])c.AcceptedMatchStatuses.Clone()
    };
}
