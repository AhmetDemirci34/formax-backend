using Formax.Application.States;
using Formax.Domain.States;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiGuardEvaluator
    {
        public AiGuardDecision Evaluate(StateDecisionContext context)
        {
            // 🔒 CONTEXT YOKSA SUS
            if (context.CurrentContextKey == AIContextKey.None)
                return AiGuardDecision.Block("NO_CONTEXT");

            // 🔒 CONFIDENCE ALTTA
            if (context.ConfidenceScore < 0.40)
                return AiGuardDecision.Block("LOW_CONFIDENCE");

            // 🔒 CONFLICT VARSA SUS
            if (context.HasConflict)
                return AiGuardDecision.Block("CONFLICTING_SIGNALS");

            return AiGuardDecision.Allow();
        }
    }
}
