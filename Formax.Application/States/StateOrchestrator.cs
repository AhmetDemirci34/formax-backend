using Formax.Domain.Entities;
using Formax.Application.AI.Memory;

namespace Formax.Application.States
{
    public sealed class StateOrchestrator
    {
        public ResolvedState Resolve(User? user)
        {
            // 1️⃣ User State
            var userState = user == null
                ? UserState.Anonymous
                : UserState.Registered;

            // 2️⃣ Premium State
            var premiumState = user != null && user.IsPremium
                ? PremiumState.Active
                : PremiumState.None;

            // 3️⃣ AI Behavior
            var aiBehaviorState =
                AiBehaviorRules.Resolve(userState, premiumState);

            // 4️⃣ Memory Decay
            if (user != null && aiBehaviorState == AiBehaviorState.Extended)
            {
                var decayEvaluator = new AiMemoryDecayEvaluator();
                var isExpired =
                    decayEvaluator.IsExpired(user.LastAiInteractionAt);

                if (!isExpired)
                {
                    aiBehaviorState = AiBehaviorState.Full;
                }
            }

            return new ResolvedState(
                userState,
                premiumState,
                aiBehaviorState
            );
        }
    }
}
