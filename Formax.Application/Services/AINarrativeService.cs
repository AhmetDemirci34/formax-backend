using System;
using Formax.Application.AI.Limits;
using Formax.Application.AI.Narrative;
using Formax.Domain.States;

namespace Formax.Application.Services
{
    public class AINarrativeService
    {
        private readonly IAINarrativeBuilder _builder;
        private readonly AINarrativeUsageTracker _usageTracker;

        public AINarrativeService(
            IAINarrativeBuilder builder,
            AINarrativeUsageTracker usageTracker)
        {
            _builder = builder;
            _usageTracker = usageTracker;
        }

        public NarrativeResult GetNarrative(
            NarrativeContext context,
            AIUxState uxState,
            AIContextKey contextKey)
        {
            if (!context.IsPremium)
            {
                var canConsume = _usageTracker.CanConsume(
                    context.UserId,
                    context.UtcNow,
                    AINarrativeUsagePolicy.FreeDailyNarrativeLimit);

                if (!canConsume)
                {
                    return new NarrativeResult
                    {
                        IsSilent = true,
                        SilentReason = "Günlük ücretsiz AI kullanım limitine ulaşıldı."
                    };
                }

                _usageTracker.Consume(context.UserId, context.UtcNow);
            }

            return _builder.Build(context, uxState, contextKey);
        }
    }
}
