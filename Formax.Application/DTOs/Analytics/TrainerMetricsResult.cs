using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Analytics
{
    public class TrainerMetricsResult
    {
        public int TotalImpressions { get; set; }
        public int TotalClicks { get; set; }
        public int TotalSkips { get; set; }
        public int TotalFollows { get; set; }

        public double CTR { get; set; }
        public double SkipRate { get; set; }
        public double FollowRate { get; set; }
    }
}