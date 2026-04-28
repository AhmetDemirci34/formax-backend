using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails;

public sealed class AiGuardDecision
{
    public bool CanProduce { get; }
    public string Reason { get; }

    private AiGuardDecision(bool canProduce, string reason)
    {
        CanProduce = canProduce;
        Reason = reason;
    }

    public static AiGuardDecision Allow()
        => new(true, "OK");

    public static AiGuardDecision Block(string reason)
        => new(false, reason);
}

