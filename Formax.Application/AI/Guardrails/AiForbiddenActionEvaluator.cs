using System.Collections.Generic;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionEvaluator
    {
        private static readonly IReadOnlyList<string> ForbiddenMarkers =
            new List<string>
            {
                AiForbiddenActions.BettingAdvice,
                AiForbiddenActions.GuaranteedOutcome,
                AiForbiddenActions.UserDirection,
                AiForbiddenActions.CouponEncouragement,
                AiForbiddenActions.ManipulativeLanguage
            };

        public bool HasForbiddenAction(IEnumerable<string> actionFlags)
        {
            foreach (var flag in actionFlags)
            {
                if (ForbiddenMarkers.Contains(flag))
                    return true;
            }

            return false;
        }
    }
}
