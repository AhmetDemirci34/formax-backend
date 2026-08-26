using System.Globalization;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;

namespace Formax.TeamStrength.Services;

public sealed class ReadResult
{
    public List<MatchRecord> Matches { get; } = new();
    public int TotalRows { get; set; }
    public int SkippedIdentity { get; set; }
    public int SkippedStatus { get; set; }
    public int SkippedMissingScore { get; set; }
    public int SkippedNotEligible { get; set; }
}

/// <summary>
/// Minimal RFC4180 reader for the model dataset. Read only: the source file is never written.
/// </summary>
public static class MatchCsvReader
{
    public static ReadResult Read(string path, TeamStrengthConfig cfg)
    {
        var res = new ReadResult();
        using var sr = new StreamReader(path);

        var headerLine = sr.ReadLine() ?? throw new InvalidOperationException("empty csv");
        var header = ParseLine(headerLine);
        if (header.Count > 0) header[0] = header[0].TrimStart('﻿');
        var ix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) ix[header[i]] = i;

        string Col(List<string> f, string name) =>
            ix.TryGetValue(name, out var i) && i < f.Count ? f[i] : string.Empty;

        var accepted = new HashSet<string>(cfg.AcceptedMatchStatuses, StringComparer.OrdinalIgnoreCase);

        while (sr.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var f = ParseLine(line);
            res.TotalRows++;

            var eligible = Col(f, "ModelEligible");
            if (!string.IsNullOrEmpty(eligible) &&
                !eligible.Equals("True", StringComparison.OrdinalIgnoreCase))
            { res.SkippedNotEligible++; continue; }

            // rule 12: an unresolved identity never enters the engine
            var conf = Col(f, "IdentityConfidence");
            if (!conf.Equals(cfg.RequiredIdentityConfidence, StringComparison.OrdinalIgnoreCase))
            { res.SkippedIdentity++; continue; }

            var status = Col(f, "MatchStatus");
            if (!accepted.Contains(status)) { res.SkippedStatus++; continue; }

            var hg = Col(f, "HomeGoals"); var ag = Col(f, "AwayGoals");
            if (!int.TryParse(hg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var homeGoals) ||
                !int.TryParse(ag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var awayGoals))
            { res.SkippedMissingScore++; continue; }

            if (!DateOnly.TryParseExact(Col(f, "Date"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            { res.SkippedMissingScore++; continue; }

            res.Matches.Add(new MatchRecord
            {
                MatchId = Col(f, "MatchId"),
                Date = date,
                Season = Col(f, "Season"),
                Competition = Col(f, "Competition"),
                CompetitionType = Col(f, "CompetitionType"),
                HomeTeamId = Col(f, "HomeTeamId"),
                AwayTeamId = Col(f, "AwayTeamId"),
                HomeTeamName = Col(f, "HomeTeam"),
                AwayTeamName = Col(f, "AwayTeam"),
                HomeGoals = homeGoals,
                AwayGoals = awayGoals,
                MatchStatus = status,
                IdentityConfidence = conf
            });
        }
        return res;
    }

    public static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
