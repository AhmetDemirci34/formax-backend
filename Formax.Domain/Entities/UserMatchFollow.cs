using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Formax.Domain.Entities
{
    public class UserMatchFollow
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int MatchId { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsLiveTrackingEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

