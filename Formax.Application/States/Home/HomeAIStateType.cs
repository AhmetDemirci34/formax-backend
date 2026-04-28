using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.States.Home
{
    public enum HomeAIStateType
    {
        SilentProtected = 0,
        Observational = 1,
        Contextual = 2,
        BackoffUncertain = 3
    }
}