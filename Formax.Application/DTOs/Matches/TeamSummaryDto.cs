using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Matches;

public class TeamSummaryDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LeagueRank { get; set; }
    public double AvgGoalsFor { get; set; }
    public double AvgGoalsAgainst { get; set; }
    public bool IsStableTeam { get; set; }
}