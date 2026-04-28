using Formax.Application.AI.Contexts;
using Formax.Application.AI.Guardrails;
using Formax.Application.AI.Limits;
using Formax.Domain.Subscriptions;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakDecisionInputBuilder
    {
        private readonly ContextInsufficientEvaluator _contextEvaluator;
        private readonly FatigueEvaluator _fatigueEvaluator;
        private readonly RepetitionEvaluator _repetitionEvaluator;
        private readonly StatePermissionEvaluator _stateEvaluator;

        public AiSpeakDecisionInputBuilder(
            ContextInsufficientEvaluator contextEvaluator,
            FatigueEvaluator fatigueEvaluator,
            RepetitionEvaluator repetitionEvaluator,
            StatePermissionEvaluator stateEvaluator)
        {
            _contextEvaluator = contextEvaluator;
            _fatigueEvaluator = fatigueEvaluator;
            _repetitionEvaluator = repetitionEvaluator;
            _stateEvaluator = stateEvaluator;
        }

        public AiSpeakDecisionInput Build(
            UnifiedAiReadContext context,
            string? nextExtendedContextKey,
            int aiUsageCountLastHour,
            System.DateTime? lastAiSpeakAtUtc)
        {
            // 🔒 FAZ-15 — AccessLevel bazlı sessizlik toleransı
            var minSilenceDuration =
                AccessLevelSilencePolicy.GetMinimumSilenceDuration(
                    context.User.AccessLevel);

            return new AiSpeakDecisionInput
            {
                // ✅ DOĞRU VE VAR OLAN YOL
                ContextInsufficient =
                    _contextEvaluator.IsInsufficient(context),

                FatigueLimitReached =
                    _fatigueEvaluator.IsFatigueLimitReached(
                        context.IsPremiumUser,
                        aiUsageCountLastHour,
                        lastAiSpeakAtUtc),

                RepetitionBlocked =
                    _repetitionEvaluator.IsRepetitionBlocked(
                        context.User.LastExtendedContextKey,
                        nextExtendedContextKey),

                StateAllowsSpeaking =
                    _stateEvaluator.IsStateAllowedToSpeak(
                        context.UserState),

                MinimumSilenceDuration = minSilenceDuration
            };
        }
    }
}
