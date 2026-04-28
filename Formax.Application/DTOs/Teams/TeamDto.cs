using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Teams
{
    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LeagueRank { get; set; }
        public double AvgGoalsFor { get; set; }
        public double AvgGoalsAgainst { get; set; }
        public bool IsStableTeam { get; set; }
        public string? LogoUrl { get; set; }
        public string? ColorPrimary { get; set; }
        public string? ColorSecondary { get; set; }
        
    }
}