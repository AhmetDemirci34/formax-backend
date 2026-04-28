
namespace Formax.Engine;
public class ResultEngine
{
    public ResultDto Evaluate(int matchId, string userPick)
    {
        int homeScore = 1;
        int awayScore = 0;
        int totalGoals = homeScore + awayScore;

        bool isWin = userPick switch
        {
            "OVER_2_5" => totalGoals > 2,
            "UNDER_2_5" => totalGoals < 3,
            _ => false
        };

        var why = new List<string>();

        if (totalGoals < 2)
            why.Add("Gol beklentisi karşılanmadı");

        if (totalGoals < 3)
            why.Add("Tempo düşük kaldı");

        if (Math.Abs(homeScore - awayScore) >= 1)
            why.Add("Maç tek taraflı geçti");

        return new ResultDto
        {
            IsWin = isWin,
            FinalScore = $"{homeScore}-{awayScore}",
            UserPick = userPick,
            Why = why,
            Impact = new ImpactDto
            {
                WinRateBefore = 65,
                WinRateAfter = isWin ? 66 : 63,
                ConfidenceBefore = 60,
                ConfidenceAfter = isWin ? 62 : 58,
                Streak = isWin ? 3 : 0
            }
        };
    }
}