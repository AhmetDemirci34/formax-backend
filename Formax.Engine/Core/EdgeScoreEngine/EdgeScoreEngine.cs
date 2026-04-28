using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.EdgeScoreEngine;

public class EdgeScoreEngine
{
    public double Calculate(double probability, double impliedProbability)
    {
        return probability - impliedProbability;
    }
}
