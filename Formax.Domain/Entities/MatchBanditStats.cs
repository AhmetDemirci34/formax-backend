using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class MatchBanditStats
    {
        public int MatchId { get; set; }

        public int Impressions { get; set; }
        public int Likes { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
