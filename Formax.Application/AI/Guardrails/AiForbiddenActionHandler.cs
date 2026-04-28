using Formax.Application.States;
using Formax.Domain.States;

namespace Formax.Application.AI.Guardrails
{
    public static class AiForbiddenActionHandler
    {
        public static AIStateResult BuildResult(string reasonCode)
        {
            return new AIStateResult
            {
                State = AIUxState.SelfRetracted,
                ShouldSpeak = false,
                CanExpand = false,
                ReasonCode = reasonCode
            };
        }
    }
}
