using System.Collections.Generic;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionContext
    {
        public IReadOnlyCollection<string> ActionFlags { get; }

        public AiForbiddenActionContext(IEnumerable<string> actionFlags)
        {
            ActionFlags = new List<string>(actionFlags);
        }
    }
}
