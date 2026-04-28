using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.ProbabilityEngine;

public class ProbabilityEngine
{
    public double Calculate(double baseProb, double trendBoost = 0)
    {
        var result = baseProb + trendBoost;

        if (result > 100) result = 100;
        if (result < 0) result = 0;

        return result;
    }
}
