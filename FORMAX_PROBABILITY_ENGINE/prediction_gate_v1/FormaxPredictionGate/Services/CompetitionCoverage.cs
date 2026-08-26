using Formax.TeamStrength.Models;

namespace Formax.Prediction.Services;

/// <summary>
/// How many matches of a competition type had already been played when a given match was predicted.
///
/// This mirrors, exactly, the counter the frozen backtest keeps inside its competition context: the
/// day-by-day replay counts a day's matches only AFTER every prediction of that day is made, so two
/// matches on the same date never count towards each other. It is recomputed here rather than read
/// out of the backtest so that no validated artefact had to be modified to add an output field.
/// </summary>
public sealed class CompetitionCoverage
{
    private readonly Dictionary<string, int> _observedAtPrediction = new(StringComparer.Ordinal);

    /// <summary>Matches of this match's competition type played on STRICTLY EARLIER days.</summary>
    public int For(string matchId) => _observedAtPrediction.TryGetValue(matchId, out var n) ? n : 0;

    public static CompetitionCoverage Build(IEnumerable<MatchRecord> matches)
    {
        var cov = new CompetitionCoverage();
        var ordered = matches
            .OrderBy(m => m.Date.DayNumber)
            .ThenBy(m => m.MatchId, StringComparer.Ordinal)
            .ToList();

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        while (i < ordered.Count)
        {
            var day = ordered[i].Date;
            var j = i;
            while (j < ordered.Count && ordered[j].Date == day) j++;

            // record what was known BEFORE this day
            for (var k = i; k < j; k++)
                cov._observedAtPrediction[ordered[k].MatchId] =
                    seen.TryGetValue(ordered[k].CompetitionType, out var n) ? n : 0;

            // only now does the day count
            for (var k = i; k < j; k++)
                seen[ordered[k].CompetitionType] = (seen.TryGetValue(ordered[k].CompetitionType, out var n) ? n : 0) + 1;

            i = j;
        }
        return cov;
    }
}
