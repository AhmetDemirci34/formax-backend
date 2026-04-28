using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Live
{
    public class MatchEventContext
    {
        public int MatchId { get; set; }
        public int Minute { get; set; }

        // Kırmızı kart
        public bool HasRedCard { get; set; }
        public string? RedCardPlayerName { get; set; }
        public string? RedCardTeamName { get; set; }

        // Penaltı
        public bool HasPenalty { get; set; }

        // VAR
        public bool IsVarReview { get; set; }
    }
}

