using System.Collections.Generic;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionGuard
    {
        private readonly AiForbiddenActionEvaluator _evaluator;

        public AiForbiddenActionGuard(AiForbiddenActionEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public AiForbiddenActionResult Check(IEnumerable<string> actionFlags)
        {
            if (_evaluator.HasForbiddenAction(actionFlags))
            {
                return AiForbiddenActionResult.Forbidden(
                    AiForbiddenActionPolicy.ResolveReason());
            }

            return AiForbiddenActionResult.Allowed();
        }
    }
}
