using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.UserIntelligenceEngine;

public class UserIntelligenceEngine
{
    public double CalculateUserFit(double accuracy, double confidence)
    {
        return (accuracy * 0.7) + (confidence * 0.3);
    }
}
