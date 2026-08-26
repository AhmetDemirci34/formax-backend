using System.Globalization;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;

namespace Formax.ModelValidation.Models;

public enum ModelId
{
    /// <summary>Reference only, carried over from V1: expanding outcome frequency, no team information.</summary>
    Simple = 0,
    TeamStrength = 1,
    IndependentPoisson = 2,
    DixonColes = 3,
    BivariatePoisson = 4
}

[Flags]
public enum ModelMask
{
    None = 0,
    Simple = 1 << ModelId.Simple,
    TeamStrength = 1 << ModelId.TeamStrength,
    IndependentPoisson = 1 << ModelId.IndependentPoisson,
    DixonColes = 1 << ModelId.DixonColes,
    BivariatePoisson = 1 << ModelId.BivariatePoisson,
    All = Simple | TeamStrength | IndependentPoisson | DixonColes | BivariatePoisson
}

public static class ModelIds
{
    public static readonly ModelId[] All =
    {
        ModelId.Simple, ModelId.TeamStrength, ModelId.IndependentPoisson,
        ModelId.DixonColes, ModelId.BivariatePoisson
    };

    /// <summary>The four models the baseline decision is made between. SIMPLE is a reference, not a candidate.</summary>
    public static readonly ModelId[] Candidates =
    {
        ModelId.TeamStrength, ModelId.IndependentPoisson, ModelId.DixonColes, ModelId.BivariatePoisson
    };

    public static string Name(ModelId id) => id switch
    {
        ModelId.Simple => "SIMPLE_BASELINE_V1",
        ModelId.TeamStrength => "TEAM_STRENGTH_BASELINE_V2",
        ModelId.IndependentPoisson => "INDEPENDENT_POISSON_V2",
        ModelId.DixonColes => "DIXON_COLES_V2",
        _ => "BIVARIATE_POISSON_V2"
    };

    public static ModelMask Mask(ModelId id) => (ModelMask)(1 << (int)id);
}

/// <summary>
/// One match, predicted by every enabled model from exactly the same pre-match state.
/// Wide format on purpose: the models must be compared PAIRED, match by match, and a long format
/// invites accidental unpaired comparisons.
/// </summary>
public sealed class MatchPrediction
{
    public required string MatchId { get; init; }
    public required DateOnly Date { get; init; }
    public required Segment Segment { get; init; }
    public required string Season { get; init; }
    public required string Competition { get; init; }
    public required string CompetitionType { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }

    public required double LambdaHome { get; init; }
    public required double LambdaAway { get; init; }
    public required double Lambda3 { get; init; }

    public required ProbTriple[] Probabilities { get; init; }   // indexed by ModelId

    public required Outcome Actual { get; init; }
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }

    public required string HomeColdStartClass { get; init; }
    public required string AwayColdStartClass { get; init; }
    public required double HomePriorWeight { get; init; }
    public required double AwayPriorWeight { get; init; }
    public required string HomePriorSource { get; init; }
    public required string AwayPriorSource { get; init; }
    public required bool IsColdStart { get; init; }
    public DateOnly? EvidenceCutoff { get; init; }

    /// <summary>Cold-start class of the side with the LESS evidence - the one that limits the prediction.</summary>
    public string WeakestColdStartClass
    {
        get
        {
            static int Rank(string c) => c switch
            {
                "NoHistory" => 0, "Limited" => 1, "Developing" => 2, "Established" => 3, "Rich" => 4, _ => 5
            };
            return Rank(HomeColdStartClass) <= Rank(AwayColdStartClass) ? HomeColdStartClass : AwayColdStartClass;
        }
    }

    private static string N(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader
    {
        get
        {
            var cols = new List<string>
            {
                "MatchId","PredictionDate","Segment","Season","Competition","CompetitionType",
                "HomeTeam","AwayTeam","LambdaHome","LambdaAway","Lambda3"
            };
            foreach (var m in ModelIds.All)
            {
                var n = ModelIds.Name(m);
                cols.Add(n + "_Home"); cols.Add(n + "_Draw"); cols.Add(n + "_Away"); cols.Add(n + "_Sum");
            }
            cols.AddRange(new[]
            {
                "ActualOutcome","HomeGoals","AwayGoals",
                "HomeColdStartClass","AwayColdStartClass","WeakestColdStartClass",
                "HomePriorWeight","AwayPriorWeight","HomePriorSource","AwayPriorSource",
                "IsColdStart","EvidenceCutoff"
            });
            return string.Join(',', cols);
        }
    }

    public string ToCsv()
    {
        static string Q(string s) => s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        var sb = new System.Text.StringBuilder(512);
        sb.Append(Q(MatchId)).Append(',').Append(Date.ToString("yyyy-MM-dd")).Append(',')
          .Append(Segment.ToString().ToUpperInvariant()).Append(',')
          .Append(Q(Season)).Append(',').Append(Q(Competition)).Append(',').Append(Q(CompetitionType)).Append(',')
          .Append(Q(HomeTeam)).Append(',').Append(Q(AwayTeam)).Append(',')
          .Append(N(LambdaHome)).Append(',').Append(N(LambdaAway)).Append(',').Append(N(Lambda3));

        foreach (var m in ModelIds.All)
        {
            var p = Probabilities[(int)m];
            sb.Append(',').Append(N(p.Home)).Append(',').Append(N(p.Draw)).Append(',').Append(N(p.Away))
              .Append(',').Append(N(p.Sum));
        }

        sb.Append(',').Append(Actual.ToString())
          .Append(',').Append(HomeGoals.ToString(CultureInfo.InvariantCulture))
          .Append(',').Append(AwayGoals.ToString(CultureInfo.InvariantCulture))
          .Append(',').Append(Q(HomeColdStartClass)).Append(',').Append(Q(AwayColdStartClass))
          .Append(',').Append(Q(WeakestColdStartClass))
          .Append(',').Append(N(HomePriorWeight)).Append(',').Append(N(AwayPriorWeight))
          .Append(',').Append(Q(HomePriorSource)).Append(',').Append(Q(AwayPriorSource))
          .Append(',').Append(IsColdStart ? "True" : "False")
          .Append(',').Append(EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty);
        return sb.ToString();
    }
}
