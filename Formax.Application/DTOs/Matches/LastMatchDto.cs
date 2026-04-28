using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Matches;

public class LastMatchDto
{
    public DateTime MatchDate { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool IsHomeMatch { get; set; }
}
