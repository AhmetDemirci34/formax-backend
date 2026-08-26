using System.Globalization;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.GatePolicy.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Models;

namespace Formax.GatePolicy.Services;

/// <summary>Everything one match contributes, assembled once and reused by every policy.</summary>
public sealed class MatchContext
{
    public required MatchPrediction Raw { get; init; }
    public required TeamStrengthSnapshot? Home { get; init; }
    public required TeamStrengthSnapshot? Away { get; init; }
    public required string IdentityConfidence { get; init; }
    public required int CompetitionMatchesObserved { get; init; }
    public required Segment Segment { get; init; }
    public required string WeakestColdStartClass { get; init; }
    /// <summary>Log loss of the model on this match. Fixed: it does not depend on any policy.</summary>
    public required double ModelLogLoss { get; init; }
}

/// <summary>One policy, measured on one slice.</summary>
public sealed class PolicyResult
{
    public required Policy Policy { get; init; }
    public required string Segment { get; init; }
    public required string Scope { get; init; }
    public required string Group { get; init; }

    public required int Total { get; init; }
    public required int Published { get; init; }
    public int Rejected => Total - Published;
    public double Coverage => Total == 0 ? double.NaN : (double)Published / Total;

    /// <summary>Selective risk: log loss over the published set. This is the GATE metric.</summary>
    public required double PublishedLogLoss { get; init; }
    public required double PublishedBrier { get; init; }
    public required double PublishedRps { get; init; }
    public required double PublishedAccuracy { get; init; }

    /// <summary>Log loss the model would have had on the matches this policy refused.</summary>
    public required double RejectedLogLoss { get; init; }
    /// <summary>Log loss over everything. The MODEL metric - identical for every policy, by construction.</summary>
    public required double AllLogLoss { get; init; }

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
    private static string Q(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

    public static string CsvHeader =>
        "Policy,MinPriorMatchesPerTeam,MinCompetitionMatchesObserved,Segment,Scope,Group," +
        "Total,Published,Rejected,CoverageRate," +
        "PublishedLogLoss,PublishedBrier,PublishedRPS,PublishedAccuracy," +
        "RejectedLogLoss,AllLogLoss";

    public string ToCsv() => string.Join(',',
        Policy.Name,
        Policy.MinPriorMatches.ToString(CultureInfo.InvariantCulture),
        Policy.MinCompetitionMatches.ToString(CultureInfo.InvariantCulture),
        Q(Segment), Q(Scope), Q(Group),
        Total.ToString(CultureInfo.InvariantCulture),
        Published.ToString(CultureInfo.InvariantCulture),
        Rejected.ToString(CultureInfo.InvariantCulture),
        F(Coverage), F(PublishedLogLoss), F(PublishedBrier), F(PublishedRps), F(PublishedAccuracy),
        F(RejectedLogLoss), F(AllLogLoss));
}

/// <summary>
/// Applies a policy through the REAL gate - the same <see cref="PredictionService"/> the output
/// contract uses - and measures what it published.
///
/// The one thing that never varies: the model probability. Every policy sees byte-identical raw
/// output and can only decide whether to let it out. <see cref="PolicyResult.AllLogLoss"/> is
/// reported on every row precisely so that this is visible rather than asserted: it must be the
/// same number in every policy's row for a given slice.
/// </summary>
public static class PolicyEvaluator
{
    public sealed class Decision
    {
        public required bool[] Published { get; init; }
        public required int[] Indices { get; init; }
    }

    /// <summary>Runs the gate over every match once for this policy. Returns the accept/reject mask.</summary>
    public static bool[] Decide(IReadOnlyList<MatchContext> ctx, Policy policy, GateConfig baseline)
    {
        var service = new PredictionService(policy.Apply(baseline));
        var published = new bool[ctx.Count];
        for (var i = 0; i < ctx.Count; i++)
        {
            var c = ctx[i];
            var dto = service.Build(c.Raw, c.Home, c.Away, c.IdentityConfidence, c.IdentityConfidence,
                c.CompetitionMatchesObserved);
            published[i] = dto.PredictionEligible;
        }
        return published;
    }

    public static PolicyResult Measure(
        IReadOnlyList<MatchContext> ctx, bool[] published, Policy policy,
        string segment, string scope, string group, Func<MatchContext, bool> include)
    {
        var pub = new MetricAccumulator(policy.Name, scope, group);
        var all = new MetricAccumulator(policy.Name, scope, group);
        double rejectedLoss = 0;
        var rejected = 0;
        var total = 0;

        for (var i = 0; i < ctx.Count; i++)
        {
            if (!include(ctx[i])) continue;
            total++;
            var p = ctx[i].Raw.Probabilities[(int)ModelId.IndependentPoisson];
            all.Add(p, ctx[i].Raw.Actual);
            if (published[i]) pub.Add(p, ctx[i].Raw.Actual);
            else { rejectedLoss += ctx[i].ModelLogLoss; rejected++; }
        }

        return new PolicyResult
        {
            Policy = policy,
            Segment = segment,
            Scope = scope,
            Group = group,
            Total = total,
            Published = total - rejected,
            PublishedLogLoss = pub.N == 0 ? double.NaN : pub.LogLoss,
            PublishedBrier = pub.N == 0 ? double.NaN : pub.Brier,
            PublishedRps = pub.N == 0 ? double.NaN : pub.Rps,
            PublishedAccuracy = pub.N == 0 ? double.NaN : pub.Accuracy,
            RejectedLogLoss = rejected == 0 ? double.NaN : rejectedLoss / rejected,
            AllLogLoss = all.N == 0 ? double.NaN : all.LogLoss
        };
    }
}
