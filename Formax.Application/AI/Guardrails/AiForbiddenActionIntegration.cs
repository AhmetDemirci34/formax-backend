using System.Collections.Generic;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionIntegration
    {
        private readonly AiForbiddenActionGuard _guard;

        public AiForbiddenActionIntegration(AiForbiddenActionGuard guard)
        {
            _guard = guard;
        }

        public AiForbiddenActionResult Evaluate(IEnumerable<string> actionFlags)
        {
            return _guard.Check(actionFlags);
        }
    }
}
