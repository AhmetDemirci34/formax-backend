using System.Globalization;
using Formax.TeamStrength.Services;

namespace Formax.DixonColes.Services;

/// <summary>Pre-match team strength as produced by the Team Strength engine. Read only.</summary>
public sealed class StrengthRow
{
    public required string MatchId { get; init; }
    public required string TeamId { get; init; }
    public required string Side { get; init; }
    public required DateOnly MatchDate { get; init; }
    public required double Attack { get; init; }
    public required double Defense { get; init; }
    public required double Overall { get; init; }
    public double? HomeStrength { get; init; }
    public double? AwayStrength { get; init; }
    public required int MatchesUsed { get; init; }
    public required string Confidence { get; init; }
    public required string ColdStartClass { get; init; }
    public required string PriorSource { get; init; }
    public required double PriorWeight { get; init; }
    public DateOnly? LastMatchDate { get; init; }
}

public static class StrengthSnapshotReader
{
    public static Dictionary<(string matchId, string side), StrengthRow> Read(string path)
    {
        var map = new Dictionary<(string, string), StrengthRow>();
        using var sr = new StreamReader(path);
        var header = MatchCsvReader.ParseLine(sr.ReadLine() ?? throw new InvalidOperationException("empty snapshot csv"));
        if (header.Count > 0) header[0] = header[0].TrimStart('﻿');
        var ix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) ix[header[i]] = i;

        string C(List<string> f, string n) => ix.TryGetValue(n, out var i) && i < f.Count ? f[i] : string.Empty;
        double D(List<string> f, string n) =>
            double.TryParse(C(f, n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
        double? DN(List<string> f, string n) =>
            double.TryParse(C(f, n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        while (sr.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var f = MatchCsvReader.ParseLine(line);
            var row = new StrengthRow
            {
                MatchId = C(f, "MatchId"),
                TeamId = C(f, "TeamId"),
                Side = C(f, "Side"),
                MatchDate = DateOnly.ParseExact(C(f, "MatchDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Attack = D(f, "AttackStrength"),
                Defense = D(f, "DefenseStrength"),
                Overall = D(f, "OverallStrength"),
                HomeStrength = DN(f, "HomeStrength"),
                AwayStrength = DN(f, "AwayStrength"),
                MatchesUsed = int.TryParse(C(f, "MatchesUsed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mu) ? mu : 0,
                Confidence = C(f, "Confidence"),
                ColdStartClass = C(f, "ColdStartClass"),
                PriorSource = C(f, "PriorSource"),
                PriorWeight = D(f, "PriorWeight"),
                LastMatchDate = DateOnly.TryParseExact(C(f, "LastMatchDate"), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var lmd) ? lmd : null
            };
            map[(row.MatchId, row.Side)] = row;
        }
        return map;
    }
}
