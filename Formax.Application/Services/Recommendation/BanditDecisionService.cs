using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BanditDecisionService
{
    private const double Epsilon = 0.2;
    private readonly Random _random = new();

    public bool IsExplore()
    {
        return _random.NextDouble() < Epsilon;
    }
}
