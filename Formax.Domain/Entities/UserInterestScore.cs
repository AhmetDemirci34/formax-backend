using System;

namespace Formax.Domain.Entities
{
    public class UserInterestScore
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        // "Team" | "League" | "ContentType"
        public string Layer { get; set; } = null!;

        // teamName / leagueName / contentKey
        public string Key { get; set; } = null!;

        public int Score { get; set; }

        public DateTime LastEventAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}