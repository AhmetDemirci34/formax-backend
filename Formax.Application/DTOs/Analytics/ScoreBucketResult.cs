using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ScoreBucketResult
{
    public double MinScore { get; set; }
    public double MaxScore { get; set; }

    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int Skips { get; set; }

    public double Ctr => Impressions == 0 ? 0 : (double)Clicks / Impressions;

    public double SkipRate => Impressions == 0 ? 0 : (double)Skips / Impressions;
}
