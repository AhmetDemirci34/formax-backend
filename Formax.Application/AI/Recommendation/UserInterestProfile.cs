using System.Collections.Generic;
using System.Linq;

public class UserInterestProfile
{
    public Dictionary<string, int> TeamScores { get; set; } = new();
    public Dictionary<string, int> LeagueScores { get; set; } = new();

    public bool HasStrongSignal()
    {
        return TeamScores.Any() || LeagueScores.Any();
    }
}