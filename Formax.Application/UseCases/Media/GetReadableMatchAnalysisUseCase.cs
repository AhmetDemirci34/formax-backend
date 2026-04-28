using Formax.Application.AI.Audit;
using Formax.Application.AI.Confidence;
using Formax.Application.AI.Guardrails;
using Formax.Application.AI.LLM.Services;
using Formax.Application.AI.Metadata;
using Formax.Application.AI.SelfAudit;
using Formax.Application.AI.World;
using Formax.Application.Common;
using Formax.Application.Common.Enums;
using Formax.Application.DTOs.AI;
using Formax.Application.DTOs.Branding;
using Formax.Application.DTOs.Common;
using Formax.Application.DTOs.Media;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.States;
using Formax.Application.UX;
using Formax.Domain.Entities;
using Formax.Domain.States;
using Formax.Domain.ValueObjects;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Media
{
    public class GetReadableMatchAnalysisUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchEventReadRepository _matchEventReadRepository;
        private readonly IAIDecisionTraceWriter _traceWriter;
        private readonly IAISelfInvalidationLogWriter _selfInvalidationWriter;
        private readonly IAIStateMachine _stateMachine;
        private readonly IAIAnalysisReadRepository _aiAnalysisReadRepository;
        private readonly IAIAnalysisWriteRepository _aiAnalysisWriteRepository;
        private readonly LLMNarrativeService _llmNarrativeService;
        private readonly WorldPerceptionProvider _worldPerceptionProvider;

        public GetReadableMatchAnalysisUseCase(
            IMatchReadRepository matchReadRepository,
            IMatchEventReadRepository matchEventReadRepository,
            IAIDecisionTraceWriter traceWriter,
            IAISelfInvalidationLogWriter selfInvalidationWriter,
            IAIStateMachine stateMachine,
            IAIAnalysisReadRepository aiAnalysisReadRepository,
            IAIAnalysisWriteRepository aiAnalysisWriteRepository,
            LLMNarrativeService llmNarrativeService,
            WorldPerceptionProvider worldPerceptionProvider)
        {
            _matchReadRepository = matchReadRepository;
            _matchEventReadRepository = matchEventReadRepository;
            _traceWriter = traceWriter;
            _selfInvalidationWriter = selfInvalidationWriter;
            _stateMachine = stateMachine;
            _aiAnalysisReadRepository = aiAnalysisReadRepository;
            _aiAnalysisWriteRepository = aiAnalysisWriteRepository;
            _llmNarrativeService = llmNarrativeService;
            _worldPerceptionProvider = worldPerceptionProvider;
        }

        public async Task<ReadableMatchAnalysisDto> Execute(
            int matchId,
            BrandProfileDto brand,
            CancellationToken cancellationToken = default)
        {
            var match = _matchReadRepository
                .Query()
                .FirstOrDefault(m => m.Id == matchId);

            if (match == null)
            {
                return new ReadableMatchAnalysisDto
                {
                    MatchId = matchId,
                    Warning = "Bu maç için analiz bulunamadı."
                };
            }

            // BASE
            var baseConfidence = 0.72;

            // 🔥 FIX: deconstruction kaldırıldı
            var (lastContextKey, lastExtendedAt) =
                _aiAnalysisReadRepository.GetLastExtendedContext(match.Id);

            var matchEvents =
                await _matchEventReadRepository.GetByMatchAsync(match.Id);

            var isLineupAnnounced =
                matchEvents.Any(e =>
                    e.EventType == "LineupAnnounced" ||
                    e.EventType == "StartingXI" ||
                    e.EventType == "Lineup");

            // 🔥 Confidence (Phase 7.5 uyumlu)
            var confidenceCalculator = new ConfidenceCalculator();

            var confidenceResult = confidenceCalculator.Calculate(
                baseConfidence,
                0, // trend
                0  // external
            );

            var confidenceScore = confidenceResult.Score;
            var confidenceLabel = confidenceResult.Label;
            var personalReason = confidenceResult.Reason;

            // GUARDRAIL
            var evaluator = new AiDecisionEvaluator();
            var guardrailDecision = evaluator.Evaluate(confidenceScore);

            var currentContextKey =
                match.Status == "Live"
                    ? AIContextKey.LiveMatchSummary
                    : match.Status == "Finished"
                        ? AIContextKey.PostMatch
                        : AIContextKey.PreMatchSummary;

            var contextMemoryWeight =
                ContextMemoryWeight.CalculateHoursDecay(
                    1.0,
                    lastExtendedAt.HasValue
                        ? (DateTime.UtcNow - lastExtendedAt.Value).TotalHours
                        : 0);

            var stateResult = _stateMachine.Decide(new StateDecisionContext
            {
                MatchId = match.Id,
                ConfidenceScore = confidenceScore,
                GuardrailBlocked = !guardrailDecision.ShouldSpeak,
                HasConflict = false,
                IsPremiumUser = brand.IsPremium,
                ContextMemoryWeight = contextMemoryWeight,
                CurrentContextKey = currentContextKey,
                LastExtendedContextKey = lastContextKey ?? AIContextKey.None,
                LastExtendedAt = lastExtendedAt
            });

            if (stateResult.State == AIUxState.Extended)
            {
                var extendedContextKey =
                    currentContextKey == AIContextKey.PreMatchSummary
                        ? AIContextKey.PreMatchExtended
                        : currentContextKey == AIContextKey.LiveMatchSummary
                            ? AIContextKey.LiveMatchExtended
                            : currentContextKey;

                await _aiAnalysisWriteRepository
                    .PersistLastExtendedContextKeyAsync(
                        match.Id,
                        extendedContextKey,
                        cancellationToken);
            }

            var rawMessage = AIStateUxTextMapper.GetMainMessage(stateResult);
            var mainMessage = AIUxSafetyGuard.EnsureSafeMessage(
                stateResult.State,
                rawMessage);

            string? llmNarrative = null;

            if (stateResult.ShouldSpeak &&
                (stateResult.State == AIUxState.Short ||
                 stateResult.State == AIUxState.Extended))
            {
                var world = _worldPerceptionProvider.GetToday(stateResult.State);

                llmNarrative = await _llmNarrativeService
                    .GenerateMatchNarrativeAsync(
                        match,
                        stateResult.State,
                        world,
                        cancellationToken);
            }

            if (!stateResult.ShouldSpeak ||
                stateResult.State == AIUxState.Silent ||
                stateResult.State == AIUxState.SelfRetracted)
            {
                _traceWriter.Write(new AIDecisionTrace
                {
                    MatchId = match.Id,
                    ConfidenceScore = confidenceScore,
                    GuardrailDecision = stateResult.ReasonCode,
                    AiBehaviorState = stateResult.State.ToString(),
                    MemoryDecayApplied = true,
                    CreatedAt = DateTime.UtcNow
                });

                return new ReadableMatchAnalysisDto
                {
                    MatchId = match.Id,
                    Warning = mainMessage,
                    MainMessage = mainMessage,
                    Headline = "AI şu an temkinli davranıyor"
                };
            }

            var isPremium = stateResult.CanExpand;

            return new ReadableMatchAnalysisDto
            {
                MatchId = match.Id,
                MainMessage = mainMessage,
                Summary = llmNarrative,
                Headline = personalReason, // 🔥 AI konuşuyor
                AITier = isPremium ? AITier.Pro : AITier.Standard,

                AIStateMeta = new AIStateMetaDto
                {
                    State = stateResult.State,
                    ReasonCode = stateResult.ReasonCode,
                    CanExpand = stateResult.CanExpand
                }
            };
        }
    }
}