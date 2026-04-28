using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Live
{
    public class LiveMatchEventDto
    {
        public int Minute { get; set; }
        public string EventType { get; set; } = null!;
        public string? Description { get; set; }
        public string? TeamName { get; set; }
        public string? PlayerName { get; set; }
    }
}

