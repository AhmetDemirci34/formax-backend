using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class UserNotification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int MatchId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

