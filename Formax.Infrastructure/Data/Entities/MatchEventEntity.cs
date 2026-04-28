using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Data.Entities
{
    public class MatchEventEntity
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string EventType { get; set; } = null!;

        public int Minute { get; set; }

        public string? TeamName { get; set; }
        public string? PlayerName { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

