using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;

namespace Formax.TeamStrength.Services;

/// <summary>Mutable per-team rating state. Never exposed outside the engine.</summary>
internal sealed class TeamState
{
    public double Atk = 1.0;
    public double Def = 1.0;
    public double HomeAtk = 1.0, HomeDef = 1.0;
    public double AwayAtk = 1.0, AwayDef = 1.0;

    public double EffMatches;
    public double EffHome, EffAway;
    public int Matches, HomeMatches, AwayMatches;

    /// <summary>Date of the last match this team actually PLAYED. Reported on the snapshot.</summary>
    public DateOnly? LastPlayed;
    /// <summary>Bookkeeping only: the date the state was last decayed to. Never reported.</summary>
    public DateOnly? AgedTo;

    public string TeamName = string.Empty;
}

/// <summary>Expanding, leakage-safe pool statistics per CompetitionType.</summary>
internal sealed class PoolState
{
    public double SumHomeGoals, SumAwayGoals;
    public int MatchCount;
    public double SumAtk, SumDef;
    public int TeamObservations;

    public double BaselineHome(TeamStrengthConfig c) =>
        MatchCount >= c.MinBaselineSamples ? SumHomeGoals / MatchCount : c.SeedBaselineHomeGoals;
    public double BaselineAway(TeamStrengthConfig c) =>
        MatchCount >= c.MinBaselineSamples ? SumAwayGoals / MatchCount : c.SeedBaselineAwayGoals;
    public bool PoolReady(TeamStrengthConfig c) => TeamObservations >= c.MinBaselineSamples;
    public double PoolAtk => TeamObservations > 0 ? SumAtk / TeamObservations : 1.0;
    public double PoolDef => TeamObservations > 0 ? SumDef / TeamObservations : 1.0;
}

public sealed class BuildResult
{
    public List<TeamStrengthSnapshot> Snapshots { get; } = new();
    public int MatchesProcessed { get; set; }
    public int TeamsProcessed { get; set; }
    public Dictionary<ColdStartClass, int> ColdStartDistribution { get; } = new();
    public Dictionary<string, int> PriorSourceDistribution { get; } = new();
}

/// <summary>
/// Dynamic, cross-competition, leakage-safe team strength.
///
/// CONTRACT
///  * A snapshot for match M is produced from matches whose date is STRICTLY earlier than M.
///    Matches played on the same calendar day never feed each other.
///  * The rating pool is global: one rating stream per canonical TeamId, fed by every FORMAX
///    competition. CompetitionType is context (baselines and pools), never a separate rating.
///  * A team with little or no history is shrunk towards a pool that is itself learned from past
///    matches only; the pool used is recorded on every snapshot.
///  * No rating is invented: with zero evidence the snapshot IS the prior and says so.
///
/// This is NOT Dixon-Coles / Bivariate Poisson. It is a multiplicative online rating; the
/// probability model is a later phase.
/// </summary>
public sealed class TeamStrengthService
{
    private readonly TeamStrengthConfig _cfg;
    private readonly Dictionary<string, TeamState> _teams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PoolState> _pools = new(StringComparer.Ordinal);

    public TeamStrengthService(TeamStrengthConfig cfg) => _cfg = cfg;

    private TeamState GetOrCreate(string teamId, string name)
    {
        if (!_teams.TryGetValue(teamId, out var s))
        {
            s = new TeamState { TeamName = name };
            _teams[teamId] = s;
        }
        if (string.IsNullOrEmpty(s.TeamName)) s.TeamName = name;
        return s;
    }

    private PoolState GetPool(string competitionType)
    {
        if (!_pools.TryGetValue(competitionType, out var p)) { p = new PoolState(); _pools[competitionType] = p; }
        return p;
    }

    private double DecayFactor(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        if (days <= 0) return 1.0;
        return Math.Pow(0.5, days / _cfg.HalfLifeDays);
    }

    /// <summary>Age the state to <paramref name="date"/>: evidence decays and indices drift back to neutral.</summary>
    private void AgeTo(TeamState s, DateOnly date)
    {
        if (s.AgedTo is null) { s.AgedTo = date; return; }
        var d = DecayFactor(s.AgedTo.Value, date);
        s.AgedTo = date;
        if (d >= 1.0) return;

        s.EffMatches *= d; s.EffHome *= d; s.EffAway *= d;
        s.Atk = 1.0 + (s.Atk - 1.0) * d;
        s.Def = 1.0 + (s.Def - 1.0) * d;
        s.HomeAtk = 1.0 + (s.HomeAtk - 1.0) * d;
        s.HomeDef = 1.0 + (s.HomeDef - 1.0) * d;
        s.AwayAtk = 1.0 + (s.AwayAtk - 1.0) * d;
        s.AwayDef = 1.0 + (s.AwayDef - 1.0) * d;
    }

    private double Clamp(double v) => Math.Clamp(v, _cfg.MinIndex, _cfg.MaxIndex);

    private TeamStrengthSnapshot Snapshot(MatchRecord m, bool home)
    {
        var teamId = home ? m.HomeTeamId : m.AwayTeamId;
        var teamName = home ? m.HomeTeamName : m.AwayTeamName;
        var s = GetOrCreate(teamId, teamName);
        AgeTo(s, m.Date);

        var pool = GetPool(m.CompetitionType);
        var prior = ColdStartPolicy.ResolvePrior(
            _cfg, m.CompetitionType, pool.PoolReady(_cfg), pool.PoolAtk, pool.PoolDef,
            pool.TeamObservations, s.Matches);

        var w = ColdStartPolicy.OwnEvidenceWeight(s.EffMatches, _cfg.ShrinkageK);
        var atk = ColdStartPolicy.Blend(s.Atk, prior.Attack, w);
        var def = ColdStartPolicy.Blend(s.Def, prior.Defense, w);

        double? homeStrength = null, awayStrength = null;
        if (s.HomeMatches > 0)
        {
            var wh = ColdStartPolicy.OwnEvidenceWeight(s.EffHome, _cfg.VenueShrinkageK);
            homeStrength = ColdStartPolicy.Blend(s.HomeAtk, prior.Attack, wh)
                         / ColdStartPolicy.Blend(s.HomeDef, prior.Defense, wh);
        }
        if (s.AwayMatches > 0)
        {
            var wa = ColdStartPolicy.OwnEvidenceWeight(s.EffAway, _cfg.VenueShrinkageK);
            awayStrength = ColdStartPolicy.Blend(s.AwayAtk, prior.Attack, wa)
                         / ColdStartPolicy.Blend(s.AwayDef, prior.Defense, wa);
        }

        var (cls, conf) = ColdStartPolicy.Classify(_cfg, s.Matches, s.EffMatches);
        var priorSource = prior.Source;

        return new TeamStrengthSnapshot
        {
            MatchId = m.MatchId,
            TeamId = teamId,
            TeamName = teamName,
            MatchDate = m.Date,
            Side = home ? "HOME" : "AWAY",
            Competition = m.Competition,
            CompetitionType = m.CompetitionType,
            AttackStrength = atk,
            DefenseStrength = def,
            OverallStrength = atk / def,
            HomeStrength = homeStrength,
            AwayStrength = awayStrength,
            MatchesUsed = s.Matches,
            EffectiveMatches = s.EffMatches,
            HomeMatchesUsed = s.HomeMatches,
            AwayMatchesUsed = s.AwayMatches,
            Confidence = conf,
            ColdStartClass = cls,
            PriorSource = priorSource,
            PriorWeight = 1.0 - w,
            LastMatchDate = s.LastPlayed
        };
    }

    private void Apply(MatchRecord m)
    {
        var h = GetOrCreate(m.HomeTeamId, m.HomeTeamName);
        var a = GetOrCreate(m.AwayTeamId, m.AwayTeamName);
        AgeTo(h, m.Date); AgeTo(a, m.Date);

        var pool = GetPool(m.CompetitionType);
        var baseHome = pool.BaselineHome(_cfg);
        var baseAway = pool.BaselineAway(_cfg);

        var expHome = baseHome * h.Atk * a.Def;
        var expAway = baseAway * a.Atk * h.Def;

        var s = _cfg.RatioSmoothing;
        var rH = (m.HomeGoals + s) / (expHome + s);
        var rA = (m.AwayGoals + s) / (expAway + s);

        var eta = _cfg.LearningRate;
        var fH = Math.Pow(rH, eta);
        var fA = Math.Pow(rA, eta);

        h.Atk = Clamp(h.Atk * fH);
        a.Def = Clamp(a.Def * fH);
        a.Atk = Clamp(a.Atk * fA);
        h.Def = Clamp(h.Def * fA);

        h.HomeAtk = Clamp(h.HomeAtk * fH);
        h.HomeDef = Clamp(h.HomeDef * fA);
        a.AwayAtk = Clamp(a.AwayAtk * fA);
        a.AwayDef = Clamp(a.AwayDef * fH);

        h.EffMatches += 1; a.EffMatches += 1;
        h.EffHome += 1; a.EffAway += 1;
        h.Matches++; a.Matches++;
        h.HomeMatches++; a.AwayMatches++;
        h.LastPlayed = m.Date; a.LastPlayed = m.Date;
        h.AgedTo = m.Date; a.AgedTo = m.Date;

        pool.SumHomeGoals += m.HomeGoals;
        pool.SumAwayGoals += m.AwayGoals;
        pool.MatchCount++;
        pool.SumAtk += h.Atk + a.Atk;
        pool.SumDef += h.Def + a.Def;
        pool.TeamObservations += 2;
    }

    /// <summary>
    /// Produces one snapshot per team per match, in chronological order.
    /// Same-day matches are snapshotted BEFORE any of that day's results are applied.
    /// </summary>
    public BuildResult Build(IEnumerable<MatchRecord> matches)
    {
        var ordered = matches
            .OrderBy(m => m.Date.DayNumber)
            .ThenBy(m => m.MatchId, StringComparer.Ordinal)
            .ToList();

        var result = new BuildResult();
        var i = 0;
        while (i < ordered.Count)
        {
            var day = ordered[i].Date;
            var j = i;
            while (j < ordered.Count && ordered[j].Date == day) j++;

            for (var k = i; k < j; k++)
            {
                var m = ordered[k];
                var sh = Snapshot(m, home: true);
                var sa = Snapshot(m, home: false);
                result.Snapshots.Add(sh);
                result.Snapshots.Add(sa);

                foreach (var snap in new[] { sh, sa })
                {
                    result.ColdStartDistribution[snap.ColdStartClass] =
                        result.ColdStartDistribution.GetValueOrDefault(snap.ColdStartClass) + 1;
                    var key = snap.PriorSource.StartsWith("POOL:", StringComparison.Ordinal)
                        ? snap.PriorSource.Split('(')[0] : snap.PriorSource;
                    result.PriorSourceDistribution[key] = result.PriorSourceDistribution.GetValueOrDefault(key) + 1;
                }
            }

            for (var k = i; k < j; k++) Apply(ordered[k]);

            result.MatchesProcessed += j - i;
            i = j;
        }

        result.TeamsProcessed = _teams.Count;
        return result;
    }

    /// <summary>Current (post-run) state of a team, for tests and diagnostics.</summary>
    public (double atk, double def, double eff, int matches, int home, int away)? PeekState(string teamId)
        => _teams.TryGetValue(teamId, out var s)
            ? (s.Atk, s.Def, s.EffMatches, s.Matches, s.HomeMatches, s.AwayMatches)
            : null;
}
