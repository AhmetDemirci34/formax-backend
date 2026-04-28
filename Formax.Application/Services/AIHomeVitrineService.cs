using Formax.Application.DTOs.AI;
using Formax.Application.AI.Narrative.Home;
using Formax.Application.AI.Narrative.Depth;
using Formax.Application.States.Home;
using Formax.Application.AI.World;

namespace Formax.Application.Services
{
    public class AIHomeVitrineService
    {
        private readonly HomeAIStateResolver _homeStateResolver;
        private readonly HomeNarrativeGuard _narrativeGuard;
        private readonly AIContentDepthResolver _depthResolver;
        private readonly IHomeNarrativeBuilder _homeNarrativeBuilder;

        public AIHomeVitrineService(
            HomeAIStateResolver homeStateResolver,
            HomeNarrativeGuard narrativeGuard,
            AIContentDepthResolver depthResolver,
            IHomeNarrativeBuilder homeNarrativeBuilder)
        {
            _homeStateResolver = homeStateResolver;
            _narrativeGuard = narrativeGuard;
            _depthResolver = depthResolver;
            _homeNarrativeBuilder = homeNarrativeBuilder;
        }

        public AIHomeVitrineDto GetVitrine(
            WorldPerceptionSummary world,
            bool isPremium)
        {
            var homeState = _homeStateResolver.Resolve(world);

            if (!_narrativeGuard.CanProduceNarrative(homeState))
            {
                if (_narrativeGuard.ShouldExplainSilence(homeState))
                {
                    return new AIHomeVitrineDto
                    {
                        Title = "FORMAX AI",
                        Message = "Bugün maçlar hakkında yorum yapmamayı tercih ediyorum. Bağlam henüz yeterince net değil.",
                        IsPremiumHint = false
                    };
                }

                return new AIHomeVitrineDto
                {
                    Title = "FORMAX AI",
                    Message = string.Empty,
                    IsPremiumHint = false
                };
            }

            var depth = _depthResolver.Resolve(isPremium);
            var message = _homeNarrativeBuilder.Build(depth);

            return new AIHomeVitrineDto
            {
                Title = "FORMAX AI",
                Message = message,
                IsPremiumHint = !isPremium && !string.IsNullOrEmpty(message)
            };
        }
    }
}
