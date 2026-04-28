using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.World
{
    public class WorldPerceptionSummary
    {
        public string Headline { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // low / medium / high
        public string ConfidenceLevel { get; set; } = "low";

        // system / external / ai
        public string Source { get; set; } = "system";
        public DateTime? LastUpdatedUtc { get; set; }
    }
}

