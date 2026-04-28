using System;

namespace Formax.Domain.Entities
{
    public class UserSessionInterest
    {
        public Guid Id { get; set; }

        public int UserId { get; set; }
        public string Key { get; set; } = ""; // team / league

        public string? League { get; set; }

        public string? Team { get; set; }

        public int Clicks { get; set; }

        public int Dwells { get; set; }

        public DateTime LastInteractionAt { get; set; }
    }
}
