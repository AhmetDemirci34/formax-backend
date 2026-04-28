using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Models;

public class MatchResult
{
    public int MatchId { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
