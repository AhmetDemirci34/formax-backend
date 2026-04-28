using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formax.Application.DTOs.Common;

namespace Formax.Application.DTOs
{
    public class AIFavoriteMatchDto
    {
        public int MatchId { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public DateTime MatchDate { get; set; }
        public double ConfidenceScore { get; set; }
        public List<ConfidenceBreakdownDto> Breakdown { get; set; } = new();
        public string Scenario { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public UserProtectionDto UserProtection { get; set; } = new();
        public string WhyThisMatch { get; set; } = string.Empty;
        public string UserProtectionNote { get; set; } = string.Empty;
        public ContentAccessDto ContentAccess { get; set; } = new();







    }
}
