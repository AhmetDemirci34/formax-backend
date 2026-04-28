using System.Collections.Generic;
using Formax.Domain.States;
using Formax.Domain.Entities;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.Common.Options;
using Formax.Application.AI.Guardrails;
using Microsoft.Extensions.Options;

namespace Formax.Application.States
{
    public class AIStateMachine : IAIStateMachine
    {
        private readonly IStateTransitionLogWriter _logWriter;
        private readonly INotificationService _notificationService;
        private readonly AIBehaviorOptions _options;
        private readonly AiGuardEvaluator _guardEvaluator;
        private readonly ILastExtendedContextKeyRepository _lastExtendedContextKeyRepository;
        private readonly AiForbiddenActionService _forbiddenActionService;

        public AIStateMachine(
            IStateTransitionLogWriter logWriter,
            INotificationService notificationService,
            IOptions<AIBehaviorOptions> options,
            AiGuardEvaluator guardEvaluator,
            ILastExtendedContextKeyRepository lastExtendedContextKeyRepository,
            AiForbiddenActionService forbiddenActionService)
        {
            _logWriter = logWriter;
            _notificationService = notificationService;
            _options = options.Value;
            _guardEvaluator = guardEvaluator;
            _lastExtendedContextKeyRepository = lastExtendedContextKeyRepository;
            _forbiddenActionService = forbiddenActionService;
        }

        public AIStateResult Decide(StateDecisionContext context)
        {
            var fromState = AIUxState.Short;

            // 🔥 MEMORY EROSION
            var effectiveConfidence =
                context.ConfidenceScore * context.ContextMemoryWeight;

            // 🔒 GUARD — TEK KAPI
            var guard = _guardEvaluator.Evaluate(context);

            if (!guard.CanProduce)
            {
                return LogAndReturn(
                    fromState,
                    AIUxState.Silent,
                    false,
                    false,
                    guard.Reason,
                    context);
            }

            // 🔒 FORBIDDEN ACTION CHECK (ŞU AN BOŞ CONTEXT)
            var forbiddenResult =
                _forbiddenActionService.Evaluate(
                    new AiForbiddenActionContext(new List<string>()));

            if (forbiddenResult.IsForbidden)
            {
                return LogAndReturn(
                    fromState,
                    AIUxState.SelfRetracted,
                    false,
                    false,
                    forbiddenResult.ReasonCode,
                    context);
            }

            AIStateResult result;

            // 🔒 ANTI-REPETITION
            if (context.LastExtendedContextKey != AIContextKey.None
                && context.LastExtendedContextKey == context.CurrentContextKey)
            {
                result = BuildResult(
                    AIUxState.Short,
                    true,
                    false,
                    "context_already_extended");
            }
            else if (effectiveConfidence < 0.40)
            {
                result = BuildResult(
                    AIUxState.Silent,
                    false,
                    false,
                    "confidence_below_threshold");
            }
            else
            {
                // 🔒 PREMIUM GATE
                result = context.IsPremiumUser
                    ? BuildResult(
                        AIUxState.Extended,
                        true,
                        true,
                        "sufficient_confidence")
                    : BuildResult(
                        AIUxState.Short,
                        true,
                        false,
                        "premium_required");
            }

            LogTransition(fromState, result, context);
            FireExtendedNotification(result, context);
            PersistLastExtendedContextIfNeeded(result, context);

            return result;
        }

        private void PersistLastExtendedContextIfNeeded(
            AIStateResult result,
            StateDecisionContext context)
        {
            if (result.State != AIUxState.Extended)
                return;

            var entity = new LastExtendedContextKey
            {
                MatchId = context.MatchId,
                ContextKey = context.CurrentContextKey,
                ExtendedAt = DateTime.UtcNow
            };

            _lastExtendedContextKeyRepository.Upsert(entity);
        }

        private AIStateResult BuildResult(
            AIUxState state,
            bool shouldSpeak,
            bool canExpand,
            string reason)
        {
            return new AIStateResult
            {
                State = state,
                ShouldSpeak = shouldSpeak,
                CanExpand = canExpand,
                ReasonCode = reason
            };
        }

        private AIStateResult LogAndReturn(
            AIUxState from,
            AIUxState to,
            bool speak,
            bool expand,
            string reason,
            StateDecisionContext context)
        {
            var result = BuildResult(to, speak, expand, reason);
            LogTransition(from, result, context);
            return result;
        }

        private void LogTransition(
            AIUxState from,
            AIStateResult result,
            StateDecisionContext context)
        {
            _logWriter.Write(new StateTransitionLog
            {
                UserId = null,
                MatchId = context.MatchId,
                FromState = from,
                ToState = result.State,
                Trigger = result.ReasonCode,
                CreatedAt = DateTime.UtcNow
            });
        }

        private void FireExtendedNotification(
            AIStateResult result,
            StateDecisionContext context)
        {
            if (result.State == AIUxState.Extended)
            {
                _ = _notificationService.NotifyAsync(
                    context.MatchId,
                    "Genişletilmiş Analiz Hazır",
                    "Bu maç için genişletilmiş AI analizi üretildi.");
            }
        }
    }
}
