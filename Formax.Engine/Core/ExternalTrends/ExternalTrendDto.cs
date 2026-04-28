using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.ExternalTrends;

public class ExternalTrendDto
{
    public double OddsMovement { get; set; }
    public double MarketConfidence { get; set; }
    public bool IsHot { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
