using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Application.Live;


namespace Formax.Application.Live
{
    public class MatchEvent
    {
        public int MatchId { get; set; }
        public MatchEventType EventType { get; set; }

        public int Minute { get; set; }

        // Opsiyonel detaylar
        public string? PlayerName { get; set; }
        public string? TeamName { get; set; }
        public string? Description { get; set; }
    }
}

