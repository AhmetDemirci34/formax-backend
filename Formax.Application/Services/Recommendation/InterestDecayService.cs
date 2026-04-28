using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Recommendation;

public class InterestDecayService
{
    public int ApplyDecay(int score, DateTime lastInteractionUtc, DateTime nowUtc)
    {
        var days = (nowUtc - lastInteractionUtc).TotalDays;

        double factor = 1.0;

        if (days > 30)
            factor = 0.20;
        else if (days > 14)
            factor = 0.50;
        else if (days > 7)
            factor = 0.70;
        else if (days > 3)
            factor = 0.90;

        return (int)Math.Round(score * factor);
    }
}
