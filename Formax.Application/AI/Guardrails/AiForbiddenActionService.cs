using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionService
    {
        private readonly AiForbiddenActionIntegration _integration;

        public AiForbiddenActionService(
            AiForbiddenActionIntegration integration)
        {
            _integration = integration;
        }

        public AiForbiddenActionResult Evaluate(
            AiForbiddenActionContext context)
        {
            return _integration.Evaluate(context.ActionFlags);
        }
    }
}

