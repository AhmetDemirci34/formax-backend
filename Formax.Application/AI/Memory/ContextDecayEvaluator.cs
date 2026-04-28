using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.AI.Memory
{
    public class ContextDecayEvaluator
    {
        // Extended anlatım tekrar eşiği (saat)
        private const int ExtendedCooldownHours = 12;

        public bool ShouldSelfRetract(
            AIUxState currentState,
            LastExtendedContextKey? lastExtendedContext)
        {
            if (currentState != AIUxState.Extended)
                return false;

            if (lastExtendedContext == null)
                return false;

            var hoursSinceExtended =
                (DateTime.UtcNow - lastExtendedContext.ExtendedAt).TotalHours;

            return hoursSinceExtended < ExtendedCooldownHours;
        }
    }
}
