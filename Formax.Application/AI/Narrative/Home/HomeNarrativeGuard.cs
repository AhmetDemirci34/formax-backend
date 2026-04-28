using Formax.Application.States.Home;

namespace Formax.Application.AI.Narrative.Home
{
    public sealed class HomeNarrativeGuard
    {
        public bool CanProduceNarrative(HomeAIState state)
        {
            if (state == null)
            {
                return false;
            }

            return state.StateType == HomeAIStateType.Contextual;
        }

        public bool ShouldExplainSilence(HomeAIState state)
        {
            if (state == null)
            {
                return true;
            }

            return state.StateType == HomeAIStateType.SilentProtected
                || state.StateType == HomeAIStateType.BackoffUncertain;
        }
    }
}
