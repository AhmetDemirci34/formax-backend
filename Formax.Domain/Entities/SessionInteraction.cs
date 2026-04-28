using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class SessionInteraction
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string? SessionId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }
    }
}
