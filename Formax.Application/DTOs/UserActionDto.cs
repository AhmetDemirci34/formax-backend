using System;

namespace Formax.Application.DTOs
{
    public class UserActionDto
    {
        public int UserId { get; set; }

        public int MatchId { get; set; }

        public int ActionType { get; set; }
        // -1 SKIP
        // 0 VIEW
        // 1 LIKE
        // 2 DETAIL
        // 3 FOLLOW

        public double Odds { get; set; }

        // 🔥 EKLENENLER
        public int ViewDurationMs { get; set; }

        public bool OpenedDetail { get; set; }

        public bool Followed { get; set; }
        public string Team { get; set; } = string.Empty;
    }
}