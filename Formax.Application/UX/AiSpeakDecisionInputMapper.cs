using Formax.Application.AI.Guardrails;
using Formax.Domain.States;

namespace Formax.Application.UX;

public sealed class AiSpeakDecisionInputMapper
{
    public AiSpeakDecisionInput Map(
        AIUxState uxState,
        bool contextInsufficient,
        bool fatigueLimitReached,
        bool repetitionBlocked,
        DateTime? lastSpeakAtUtc,
        TimeSpan minimumSilenceDuration)
    {
        return new AiSpeakDecisionInput
        {
            ContextInsufficient = contextInsufficient,
            FatigueLimitReached = fatigueLimitReached,
            RepetitionBlocked = repetitionBlocked,
            StateAllowsSpeaking = uxState != AIUxState.Silent,
            LastSpeakAtUtc = lastSpeakAtUtc,
            MinimumSilenceDuration = minimumSilenceDuration
        };
    }
}
