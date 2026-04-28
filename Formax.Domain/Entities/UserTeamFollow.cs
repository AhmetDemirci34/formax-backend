using System;

namespace Formax.Domain.Entities
{
    public class UserTeamFollow
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int TeamId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
