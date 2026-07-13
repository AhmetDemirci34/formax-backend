using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — central weight table for signals. Weight
    /// orders signals within a match; the highest becomes the primary signal.
    /// </summary>
    public static class MatchSignalWeight
    {
        public const double Final = 100;
        public const double Derby = 95;
        public const double Playoff = 90;
        public const double TitleRace = 80;
        public const double RelegationBattle = 80;
        public const double HighImportance = 75;
        public const double Rivalry = 70;
        public const double FormAdvantage = 65;
        public const double HistoricalDominance = 65;
        public const double StrongForm = 60;
        public const double NewsMomentum = 60;
        public const double NewsDrivenMatch = 50;
        public const double FollowAttention = 55;
        public const double NewsAttention = 55;
        public const double MarketAttention = 55;
        public const double ImportantMatch = 50;
        public const double UserAttention = 50;
        public const double BalancedRivalry = 45;
        public const double SourceConfidence = 45;
        public const double WeakForm = 40;

        public static double For(MatchSignalType type) => type switch
        {
            MatchSignalType.Final => Final,
            MatchSignalType.Derby => Derby,
            MatchSignalType.Playoff => Playoff,
            MatchSignalType.TitleRace => TitleRace,
            MatchSignalType.RelegationBattle => RelegationBattle,
            MatchSignalType.HighImportance => HighImportance,
            MatchSignalType.Rivalry => Rivalry,
            MatchSignalType.FormAdvantage => FormAdvantage,
            MatchSignalType.HistoricalDominance => HistoricalDominance,
            MatchSignalType.StrongForm => StrongForm,
            MatchSignalType.NewsMomentum => NewsMomentum,
            MatchSignalType.NewsDrivenMatch => NewsDrivenMatch,
            MatchSignalType.MarketAttention => MarketAttention,
            MatchSignalType.FollowAttention => FollowAttention,
            MatchSignalType.NewsAttention => NewsAttention,
            MatchSignalType.ImportantMatch => ImportantMatch,
            MatchSignalType.UserAttention => UserAttention,
            MatchSignalType.BalancedRivalry => BalancedRivalry,
            MatchSignalType.SourceConfidence => SourceConfidence,
            MatchSignalType.WeakForm => WeakForm,
            _ => 0
        };
    }
}
