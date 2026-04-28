using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.States;

public static class AiBehaviorRules
{
    public static AiBehaviorState Resolve(
        UserState userState,
        PremiumState premiumState)
    {
        if (userState == UserState.Anonymous)
            return AiBehaviorState.Basic;

        if (premiumState == PremiumState.Active)
            return AiBehaviorState.Extended;

        return AiBehaviorState.Full;
    }
}

