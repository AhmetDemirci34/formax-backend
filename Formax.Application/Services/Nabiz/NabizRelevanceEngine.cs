using Formax.Application.DTOs.Nabiz;
using Formax.Domain.Entities;
using Regex = System.Text.RegularExpressions.Regex;

namespace Formax.Application.Services.Nabiz;

public class NabizRelevanceEngine
{
    private static readonly Dictionary<string, string[]> TeamAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arsenal"] = ["The Gunners", "AFC"],
            ["Chelsea"] = ["CFC", "Blues", "The Blues"],
            ["Liverpool"] = ["LFC", "The Reds", "Reds"],
            ["Manchester United"] = ["Man Utd", "Man United", "MUFC", "Red Devils"],
            ["Manchester City"] = ["Man City", "MCFC", "Citizens"],
            ["Tottenham"] = ["Spurs", "THFC", "Tottenham Hotspur"],
            ["Newcastle United"] = ["Newcastle", "NUFC", "Magpies", "Toon"],
            ["Aston Villa"] = ["Villa", "AVFC"],
            ["West Ham United"] = ["West Ham", "Hammers", "WHU"],

            ["Barcelona"] = ["Barca", "FCB", "Blaugrana", "Barça"],
            ["Real Madrid"] = ["Madrid", "Merengues", "RMCF"],
            ["Atletico Madrid"] = ["Atletico", "Atleti", "ATM", "Atlético"],

            ["Bayern Munich"] = ["Bayern", "FC Bayern"],
            ["Borussia Dortmund"] = ["BVB", "Dortmund"],

            ["Juventus"] = ["Juve"],
            ["AC Milan"] = ["Milan"],
            ["Inter Milan"] = ["Inter"],
            ["Napoli"] = ["SSC Napoli"],
            ["AS Roma"] = ["Roma"],

            ["Paris Saint-Germain"] = ["PSG"],

            ["Beşiktaş"] = ["Besiktas", "BJK"],
            ["Galatasaray"] = ["Gala", "GS"],
            ["Fenerbahçe"] = ["Fener", "Fenerbahce", "FB"],
            ["Trabzonspor"] = ["Trabzon", "TS"]
        };

    public (int? MatchId, double RelevanceScore) FindBestMatch(
        NabizRawItem item,
        IEnumerable<Match> candidates,
        Dictionary<int, string> teamNames)
    {
        int? bestMatchId = null;
        double bestScore = 0.0;

        foreach (var match in candidates)
        {
            double score = 0.0;

            if (teamNames.TryGetValue(match.HomeTeamId, out var homeName))
                score += ScoreTeam(homeName, item.Headline, item.Summary);

            if (teamNames.TryGetValue(match.AwayTeamId, out var awayName))
                score += ScoreTeam(awayName, item.Headline, item.Summary);

            if (score > 0 && !string.IsNullOrWhiteSpace(match.League))
            {
                var leagueLower = match.League.ToLowerInvariant();
                var haystack = $"{item.Headline} {item.Summary}".ToLowerInvariant();

                if (haystack.Contains(leagueLower))
                    score += 0.3;
            }

            score = Math.Min(score, 1.0);

            if (score >= 0.3 && score > bestScore)
            {
                bestScore = score;
                bestMatchId = match.Id;
            }
        }

        return (bestMatchId, bestScore);
    }

    private static double ScoreTeam(string teamName, string headline, string summary)
    {
        double score = 0.0;

        var team = teamName.ToLowerInvariant();
        var h = headline.ToLowerInvariant();
        var s = summary.ToLowerInvariant();

        if (h.Contains(team)) score += 0.8;
        else if (s.Contains(team)) score += 0.4;

        if (TeamAliases.TryGetValue(teamName, out var aliases))
        {
            foreach (var alias in aliases)
            {
                var token = alias.ToLowerInvariant();

                if (MatchesWholeWord(h, token))
                {
                    score += 0.5;
                    break;
                }

                if (MatchesWholeWord(s, token))
                {
                    score += 0.25;
                    break;
                }
            }
        }

        return score;
    }

    private static bool MatchesWholeWord(string haystack, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return Regex.IsMatch(haystack, $@"\b{Regex.Escape(token)}\b");
    }
}