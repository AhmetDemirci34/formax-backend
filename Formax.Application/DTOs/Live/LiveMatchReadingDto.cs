using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Live
{
    public class LiveMatchReadingDto
    {
        public int MatchId { get; set; }
        public int MatchMinute { get; set; }
        public string MatchState { get; set; } = string.Empty;
        public string CurrentFlow { get; set; } = string.Empty;
        public string PossibleScenario { get; set; } = string.Empty;
        public string LowProbabilityNote { get; set; } = string.Empty;
        public string UserWarning { get; set; } = string.Empty;
        public bool IsPremiumContent { get; set; }
        public string? LastEventType { get; set; }
    }
}

