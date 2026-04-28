using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Recommendation;


public class RankedMatchResult
{
    public int MatchId { get; set; }
    public double Score { get; set; }
    public string TeamA { get; set; } = default!;
    public string TeamB { get; set; } = default!;
}
