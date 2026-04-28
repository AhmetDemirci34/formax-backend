using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.AI.Contexts
{
    public class AIUxStateResolver
    {
        public AIUxState Resolve(
            Match match,
            LastExtendedContextKey? lastExtendedContext)
        {
            var now = DateTime.UtcNow;
            var hoursToMatch = (match.MatchDate - now).TotalHours;

            if (match.Status == "Live")
                return AIUxState.Extended;

            if (hoursToMatch <= 6)
            {
                if (lastExtendedContext != null)
                    return AIUxState.Short;

                return AIUxState.Extended;
            }

            return AIUxState.Silent;
        }
    }
}
