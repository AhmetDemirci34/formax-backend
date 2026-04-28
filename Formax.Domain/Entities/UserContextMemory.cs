using System;

namespace Formax.Domain.Entities
{
    public class UserContextMemory
    {
        public Guid Id { get; set; }

        public int UserId { get; set; }

        public string? LastLeague { get; set; }

        public string? LastTeam { get; set; }

        public string? LastContentType { get; set; }

        public int RecentClicks { get; set; }

        public int RecentDwells { get; set; }

        public DateTime LastInteractionAt { get; set; }
    }
}
