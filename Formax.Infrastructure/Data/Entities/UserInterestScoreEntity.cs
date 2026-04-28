using System;

namespace Formax.Infrastructure.Data.Entities
{
    public class UserInterestScoreEntity
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Layer { get; set; } = null!;

        public string Key { get; set; } = null!;

        public int Score { get; set; }

        public DateTime LastEventAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}