using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Contexts
{
    public class AiReadContext
    {
        public string MatchSummary { get; set; } = null!;
        public string TimelineSummary { get; set; } = null!;
        public int CurrentMinute { get; set; }
    }
}
