using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.States;

public sealed class ResolvedState
{
    public UserState UserState { get; }
    public PremiumState PremiumState { get; }
    public AiBehaviorState AiBehaviorState { get; }

    public ResolvedState(
        UserState userState,
        PremiumState premiumState,
        AiBehaviorState aiBehaviorState)
    {
        UserState = userState;
        PremiumState = premiumState;
        AiBehaviorState = aiBehaviorState;
    }
}

