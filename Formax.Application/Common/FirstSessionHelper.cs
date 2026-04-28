using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Common;

public static class FirstSessionHelper
{
    public static bool IsFirstSession(int totalInteractions)
    {
        return totalInteractions < 5;
    }
}