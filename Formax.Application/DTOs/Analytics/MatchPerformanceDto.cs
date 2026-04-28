using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MatchPerformanceDto
{
    public int MatchId { get; set; }

    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int Skips { get; set; }
    public int Follows { get; set; }

    public double CTR { get; set; }
    public double SkipRate { get; set; }
}
