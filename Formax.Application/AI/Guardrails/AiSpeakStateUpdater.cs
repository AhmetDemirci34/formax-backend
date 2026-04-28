using Formax.Application.AI.Contexts;
using Formax.Domain.Entities;
using Formax.Domain.States;
using Formax.Application.States;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakStateUpdater
    {
        private readonly IStateTransitionLogWriter _logWriter;

        public AiSpeakStateUpdater(IStateTransitionLogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public void Update(
            UserExperienceContext ctx,
            AiSpeakDecisionResult decision,
            int? userId,
            int? matchId)
        {
            var previousSpeak = ctx.AiSpeakState;
            var nextSpeak = ResolveNextSpeakState(previousSpeak, decision);

            if (previousSpeak == nextSpeak)
                return;

            ctx.AiSpeakState = nextSpeak;

            if (!matchId.HasValue)
                return;

            var log = new StateTransitionLog
            {
                UserId = userId is null ? null : userId.Value,
                MatchId = matchId.Value,
                FromState = MapToUxState(previousSpeak),
                ToState = MapToUxState(nextSpeak),
                Trigger = decision.Reason?.ToString() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };



            _logWriter.Write(log);
        }

        private static AiSpeakState ResolveNextSpeakState(
            AiSpeakState previous,
            AiSpeakDecisionResult decision)
        {
            if (!decision.CanSpeak)
            {
                if (decision.Reason == AiSpeakReason.RateLimited)
                    return AiSpeakState.Cooldown;

                return AiSpeakState.Silent;
            }

            return previous switch
            {
                AiSpeakState.Initial => AiSpeakState.SoftRead,
                AiSpeakState.SoftRead => AiSpeakState.DeepRead,
                _ => AiSpeakState.DeepRead
            };
        }

        private static AIUxState MapToUxState(AiSpeakState speakState)
        {
            return speakState switch
            {
                AiSpeakState.Initial => AIUxState.Silent,
                AiSpeakState.Silent => AIUxState.Silent,
                AiSpeakState.SoftRead => AIUxState.Short,
                AiSpeakState.DeepRead => AIUxState.Extended,
                AiSpeakState.Cooldown => AIUxState.SelfRetracted,
                _ => AIUxState.Silent
            };
        }
    }
}
