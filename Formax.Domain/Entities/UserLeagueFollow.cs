using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Kullanıcının takip ettiği lig. Mimari, UserTeamFollow ile birebir aynıdır
    /// (soft-active). Lig, Match.LeagueId ile aynı kimliği kullanır.
    /// </summary>
    public class UserLeagueFollow
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int LeagueId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
