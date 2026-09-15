using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Enums;
using Formax.Application.AI.Guardrails;
using Formax.Application.AI.Templates;
using Formax.Application.AI.UX;
using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Application.Live;
using Formax.Application.Services;
using Formax.Application.UX;
using Formax.Domain.Entities;
using Formax.Domain.Subscriptions;

namespace Formax.Application.UseCases.Live
{
    public class GetLiveMatchAiAnalysisUseCase
    {
        private readonly AiDepthResolver _depthResolver;
        private readonly UserExperienceUpdater _experienceUpdater;
        private readonly UserExperienceContextFactory _userExperienceContextFactory;
        private readonly AiSpeakDecisionInputBuilder _decisionInputBuilder;
        private readonly AiSpeakDecisionService _decisionService;

        // 🔒 FAZ-12.6 — TELEMETRY (PASİF)
        private readonly IAiSpeakTelemetryRepository _telemetryRepository;

        // 🔒 FAZ-15 — ACCESS
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IIntroAccessRepository _introAccessRepository;

        public GetLiveMatchAiAnalysisUseCase(
            AiDepthResolver depthResolver,
            UserExperienceUpdater experienceUpdater,
            UserExperienceContextFactory userExperienceContextFactory,
            AiSpeakDecisionInputBuilder decisionInputBuilder,
            AiSpeakDecisionService decisionService,
            IAiSpeakTelemetryRepository telemetryRepository,
            ISubscriptionRepository subscriptionRepository,
            IIntroAccessRepository introAccessRepository)
        {
            _depthResolver = depthResolver;
            _experienceUpdater = experienceUpdater;
            _userExperienceContextFactory = userExperienceContextFactory;
            _decisionInputBuilder = decisionInputBuilder;
            _decisionService = decisionService;
            _telemetryRepository = telemetryRepository;
            _subscriptionRepository = subscriptionRepository;
            _introAccessRepository = introAccessRepository;
        }

        public async Task<LiveMatchAiAnalysisResponseDto> ExecuteAsync(
            int matchId,
            UserExperienceContext ctx)
        {
            // 1️⃣ READ CONTEXT (STATİK / PASİF)
            var aiReadContext = new AiReadContext
            {
                MatchSummary = string.Empty,
                TimelineSummary = string.Empty,
                CurrentMinute = 0
            };

            var recentEvents = new List<MatchEvent>();

            var unifiedContext =
                await _userExperienceContextFactory.CreateUnifiedAiReadContextAsync(
                    matchId,
                    aiReadContext,
                    ctx,
                    recentEvents
                );

            var decisionInput =
                _decisionInputBuilder.Build(
                    unifiedContext,
                    nextExtendedContextKey: null,
                    aiUsageCountLastHour: unifiedContext.AiUsageCount,
                    lastAiSpeakAtUtc: null
                );

            var decision = _decisionService.Decide(decisionInput);

            // 2️⃣ USER ID (GUID)
            Guid? userGuid =
                ctx.UserId.HasValue
                    ? Guid.Parse(ctx.UserId.Value.ToString())
                    : null;

            // 3️⃣ ACCESS
            var subscription =
                userGuid.HasValue
                    ? await _subscriptionRepository.GetActiveByUserIdAsync(userGuid.Value)
                    : null;

            var introAccess =
                userGuid.HasValue
                    ? await _introAccessRepository.GetByUserIdAsync(userGuid.Value)
                    : null;

            var accessLevel =
                AccessLevelResolver.Resolve(
                    subscription,
                    introAccess,
                    DateTime.UtcNow);

            // 🔒 FAZ-15 — TELEMETRY (NET & ENUM UYUMLU)
            try
            {
                _telemetryRepository.Add(new AiSpeakTelemetry
                {
                    UserId = userGuid,
                    MatchId = matchId,
                    CanSpeak = decision.CanSpeak,
                    SilenceReason = decision.SilenceReason,
                    AccessLevel = accessLevel, // 🔒 ENUM
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            catch { }

            if (!decision.CanSpeak)
            {
                return new LiveMatchAiAnalysisResponseDto
                {
                    AnalysisText = null,
                    AdditionalContextAvailable = false,
                    SilenceReason = decision.SilenceReason,
                    SilenceMessage =
                        SilenceReasonUxMap.GetMessage(decision.SilenceReason),
                    NextAllowedAtUtc = decision.NextAllowedAtUtc
                };
            }

            // 4️⃣ AI DEPTH
            ctx.AiDepthLevel = _depthResolver.Resolve(accessLevel);

            var analysisText =
                AiUiTextMap.GetAnalysisText(ctx.AiDepthLevel);

            // 5️⃣ EXPERIENCE UPDATE
            _experienceUpdater.OnAiResponseProduced(
                ctx,
                isNewMatch: true);

            // 6️⃣ REGISTER HINT
            bool showHint =
                AiUiTextMap.ShouldShowRegisterHint(ctx.AiDepthLevel)
                && !ctx.IsRegistered
                && !ctx.HasSeenRegisterHint;

            if (showHint)
                ctx.HasSeenRegisterHint = true;

            // 7️⃣ EXTENDED CONTEXT
            bool canShowExtendedContext =
                ctx.CanExpandAiContext
                && ctx.ExtendedContextShownCount < 2;

            string? extendedContextKey = null;

            if (canShowExtendedContext)
            {
                extendedContextKey =
                    ctx.LastExtendedContextKey == "tempo_psikoloji"
                        ? "ikinci_yari_davranis"
                        : "tempo_psikoloji";

                ctx.LastExtendedContextKey = extendedContextKey;
                ctx.ExtendedContextShownCount++;
            }

            return new LiveMatchAiAnalysisResponseDto
            {
                AnalysisText = analysisText,
                ShowRegisterHint = showHint,
                HintMessage = showHint
                    ? "Analizleri kaydedip bu bağlamı korumak ister misin?"
                    : null,
                AdditionalContextAvailable = ctx.CanExpandAiContext,
                AdditionalContextInfo =
                    AiUiTextMap.GetAdditionalContextInfo(ctx.CanExpandAiContext),
                ContinuityHint =
                    AiUiTextMap.GetContinuityHint(
                        ctx.LastExtendedContextKey,
                        extendedContextKey),
                ExtendedContextBlocks =
                    canShowExtendedContext && extendedContextKey != null
                        ? AiUiTextMap.ExtendedContextMap[extendedContextKey]
                        : null
            };
        }
    }
}
