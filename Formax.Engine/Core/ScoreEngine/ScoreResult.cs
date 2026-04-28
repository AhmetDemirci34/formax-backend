using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.ScoreEngine;

public class ScoreResult
{
    public double Probability { get; set; }
    public double Edge { get; set; }
    public double UserFit { get; set; }
    public double FinalScore { get; set; }
}