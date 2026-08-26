using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Formax.TeamStrength.Models;

namespace Formax.ModelValidation.Config;

public enum Segment { Train = 0, Validation = 1, Test = 2 }

/// <summary>
/// The chronological split, loaded from split.config.json.
///
/// It is a pure function of the match DATE: everything before <see cref="ValidationStart"/> is
/// TRAIN, everything before <see cref="TestStart"/> is VALIDATION, the rest is TEST. There is no
/// random assignment, no shuffling and no season-label logic (the 2019/20 and 2020/21 labels
/// overlap in time, so labels cannot define a chronological boundary).
///
/// The split is fixed before the first candidate is evaluated. Nothing in the tuning path is
/// allowed to read a match at or after <see cref="TestStart"/> - that is enforced by
/// <see cref="TestFence"/> and audited in the leakage tests.
/// </summary>
public sealed class SplitConfig
{
    public string SplitVersion { get; set; } = "SPLIT_V2_SEASONAL_5_2_2";
    public string ValidationStart { get; set; } = "2022-06-01";
    public string TestStart { get; set; } = "2024-06-15";
    public string SelectionMetric { get; set; } = "LogLoss";
    public string SelectionModel { get; set; } = "INDEPENDENT_POISSON";

    [JsonIgnore] public DateOnly ValidationStartDate { get; private set; }
    [JsonIgnore] public DateOnly TestStartDate { get; private set; }
    [JsonIgnore] public string SourcePath { get; private set; } = "(defaults)";

    public static SplitConfig Load(string path)
    {
        SplitConfig cfg;
        if (File.Exists(path))
        {
            cfg = JsonSerializer.Deserialize<SplitConfig>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? throw new InvalidOperationException("split config could not be parsed");
            cfg.SourcePath = path;
        }
        else cfg = new SplitConfig();

        cfg.ValidationStartDate = DateOnly.ParseExact(cfg.ValidationStart, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        cfg.TestStartDate = DateOnly.ParseExact(cfg.TestStart, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cfg.TestStartDate <= cfg.ValidationStartDate)
            throw new InvalidOperationException("test must start after validation");
        return cfg;
    }

    public Segment Of(DateOnly date)
        => date < ValidationStartDate ? Segment.Train
         : date < TestStartDate ? Segment.Validation
         : Segment.Test;

    /// <summary>Everything a parameter search is allowed to look at: strictly before the test boundary.</summary>
    public bool IsSelectable(DateOnly date) => date < TestStartDate;

    public string Describe() =>
        $"{SplitVersion} | train < {ValidationStart} | validation < {TestStart} | test >= {TestStart} | " +
        $"selection = {SelectionMetric} of {SelectionModel} on VALIDATION | source = {SourcePath}";

    /// <summary>
    /// Proves the boundaries fall in real calendar gaps: reports the last match before and the first
    /// match after each boundary, so nobody has to trust the dates on faith.
    /// </summary>
    public string DescribeBoundaries(IReadOnlyList<MatchRecord> matches)
    {
        string Gap(DateOnly boundary)
        {
            var before = matches.Where(m => m.Date < boundary).Select(m => m.Date).DefaultIfEmpty().Max();
            var after = matches.Where(m => m.Date >= boundary).Select(m => m.Date).DefaultIfEmpty().Min();
            return $"{before:yyyy-MM-dd} -> [{boundary:yyyy-MM-dd}] -> {after:yyyy-MM-dd}";
        }
        return $"validation boundary: {Gap(ValidationStartDate)}   test boundary: {Gap(TestStartDate)}";
    }
}

/// <summary>
/// A hard fence around the test set, shared by every parameter search.
///
/// Two jobs. It FILTERS: the only match list a search ever receives comes out of
/// <see cref="Selectable"/>, which drops everything at or after the test boundary and counts what
/// it dropped. And it WITNESSES: every match a search scores is passed to
/// <see cref="RecordScored"/>, which throws on a breach and keeps the latest date ever scored, so
/// the audit can report a measured date instead of an intention.
/// </summary>
public sealed class TestFence
{
    private readonly SplitConfig _split;
    private long _scored;
    private long _withheld;
    private int _searches;
    private int _breaches;
    private int _latestDay = int.MinValue;

    public TestFence(SplitConfig split) => _split = split;

    public long MatchesScored => Interlocked.Read(ref _scored);
    public long MatchesWithheld => Interlocked.Read(ref _withheld);
    public int SearchesRun => Volatile.Read(ref _searches);
    public int Breaches => Volatile.Read(ref _breaches);
    /// <summary>The latest match date any parameter search was ever allowed to see. Must stay below the test boundary.</summary>
    public DateOnly? LatestDateScored
    {
        get
        {
            var d = Volatile.Read(ref _latestDay);
            return d == int.MinValue ? null : DateOnly.FromDayNumber(d);
        }
    }

    public IReadOnlyList<MatchRecord> Selectable(IReadOnlyList<MatchRecord> all)
    {
        var kept = new List<MatchRecord>(all.Count);
        var withheld = 0;
        foreach (var m in all)
        {
            if (_split.IsSelectable(m.Date)) kept.Add(m);
            else withheld++;
        }
        Interlocked.Increment(ref _searches);
        Interlocked.Add(ref _withheld, withheld);
        return kept;
    }

    /// <summary>
    /// Called for EVERY match every parameter search scores - lock free, because it is called
    /// millions of times. A date at or after the test boundary throws immediately.
    /// </summary>
    public void RecordScored(string matchId, DateOnly date)
    {
        if (!_split.IsSelectable(date))
        {
            Interlocked.Increment(ref _breaches);
            throw new InvalidOperationException(
                $"TEST FENCE BREACH: parameter search scored {matchId} dated {date:yyyy-MM-dd}");
        }

        Interlocked.Increment(ref _scored);

        var day = date.DayNumber;
        int current;
        while (day > (current = Volatile.Read(ref _latestDay)))
            Interlocked.CompareExchange(ref _latestDay, day, current);
    }

    public string Describe()
        => $"searches={SearchesRun} scored={MatchesScored} withheld={MatchesWithheld} " +
           $"latestDateScored={LatestDateScored?.ToString("yyyy-MM-dd") ?? "(none)"} " +
           $"testBoundary={_split.TestStart} breaches={Breaches}";
}
