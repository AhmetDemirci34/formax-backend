using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class GlobalTrend
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public double LikeRate { get; set; }
        public double SkipRate { get; set; }

        public double Score { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
