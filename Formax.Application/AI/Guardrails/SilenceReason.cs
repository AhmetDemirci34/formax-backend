using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public enum SilenceReason
    {
        None = 0,
        ContextInsufficient = 1,
        FatigueLimitReached = 2,
        RepetitionBlocked = 3,
        StateDenied = 4
    }
}

