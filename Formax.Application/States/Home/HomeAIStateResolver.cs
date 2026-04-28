using Formax.Application.AI.World;

namespace Formax.Application.States.Home
{
    public sealed class HomeAIStateResolver
    {
        public HomeAIState Resolve(WorldPerceptionSummary world)
        {
            if (world == null)
            {
                return new HomeAIState(HomeAIStateType.SilentProtected);
            }

            // FAZ-4.3: şimdilik bağ var, yorum sınırlı
            return new HomeAIState(HomeAIStateType.Observational);
        }
    }
}
