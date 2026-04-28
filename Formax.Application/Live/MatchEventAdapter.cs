using Formax.Domain.Entities;

namespace Formax.Application.Live
{
    public static class MatchEventAdapter
    {
        public static MatchEventContext From(Match match)
        {
            return new MatchEventContext
            {
                MatchId = match.Id,
                Minute = TryMinute(match.MatchMinute),

                // ŞİMDİLİK simülasyon / dış kaynağa hazır
                HasRedCard = false,
                RedCardPlayerName = null,
                RedCardTeamName = null,

                HasPenalty = false,
                IsVarReview = false
            };
        }

        private static int TryMinute(string? minute)
        {
            return int.TryParse(minute, out var m) ? m : 0;
        }
    }
}

