using Formax.Application.AI.Audit;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Memory;
using Formax.Application.AI.SelfAudit;
using Formax.Application.Common;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.States;
using Formax.Application.DTOs.Matches;
using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.UseCases
{
    public class GetMatchDetailAIContextUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IAIDecisionTraceWriter _aiDecisionTraceWriter;
        private readonly IStateTransitionLogWriter _stateTransitionLogWriter;
        private readonly IAISelfInvalidationLogWriter _aiSelfInvalidationLogWriter;
        private readonly ILastExtendedContextKeyRepository _lastExtendedContextKeyRepository;
        private readonly AIUxStateResolver _aiUxStateResolver;
        private readonly ContextDecayEvaluator _contextDecayEvaluator;
        private readonly ISapmaMotor _sapmaMotor;
        private readonly ITeamReadRepository _teamReadRepository;

        public GetMatchDetailAIContextUseCase(
            IMatchReadRepository matchReadRepository,
            IAIDecisionTraceWriter aiDecisionTraceWriter,
            IStateTransitionLogWriter stateTransitionLogWriter,
            IAISelfInvalidationLogWriter aiSelfInvalidationLogWriter,
            ILastExtendedContextKeyRepository lastExtendedContextKeyRepository,
            AIUxStateResolver aiUxStateResolver,
            ContextDecayEvaluator contextDecayEvaluator,
            ISapmaMotor sapmaMotor,
            ITeamReadRepository teamReadRepository)
        {
            _matchReadRepository = matchReadRepository;
            _aiDecisionTraceWriter = aiDecisionTraceWriter;
            _stateTransitionLogWriter = stateTransitionLogWriter;
            _aiSelfInvalidationLogWriter = aiSelfInvalidationLogWriter;
            _lastExtendedContextKeyRepository = lastExtendedContextKeyRepository;
            _aiUxStateResolver = aiUxStateResolver;
            _contextDecayEvaluator = contextDecayEvaluator;
            _sapmaMotor = sapmaMotor;
            _teamReadRepository = teamReadRepository;
        }

        public object? Execute(int matchId)
        {
            var match = _matchReadRepository
                .Query()
                .FirstOrDefault(x => x.Id == matchId);

            if (match == null)
                return null;

            var now = DateTime.UtcNow;

            // =====================================
            // LAST EXTENDED CONTEXT (MEMORY)
            // =====================================
            var lastExtendedContext =
                _lastExtendedContextKeyRepository.GetByMatchId(match.Id);

            // =====================================
            // AI UX STATE (TEK MERKEZ)
            // =====================================
            var resolvedState =
                _aiUxStateResolver.Resolve(match, lastExtendedContext);

            var aiUxState = resolvedState;

            // =====================================
            // CONTEXT DECAY / SELF RETRACT
            // =====================================
            if (_contextDecayEvaluator.ShouldSelfRetract(aiUxState, lastExtendedContext))
            {
                aiUxState = AIUxState.SelfRetracted;

                _aiSelfInvalidationLogWriter.Write(new AISelfInvalidationLog
                {
                    MatchId = match.Id,
                    Reason = "extended_context_recently_used",
                    CreatedAt = now
                });
            }

            // =====================================
            // STATE TRANSITION LOG
            // =====================================
            if (aiUxState != resolvedState)
            {
                _stateTransitionLogWriter.Write(new StateTransitionLog
                {
                    MatchId = match.Id,
                    FromState = resolvedState,
                    ToState = aiUxState,
                    Trigger = "context_decay",
                    CreatedAt = now
                });
            }
            else
            {
                _stateTransitionLogWriter.Write(new StateTransitionLog
                {
                    MatchId = match.Id,
                    FromState = AIUxState.Silent,
                    ToState = aiUxState,
                    Trigger = "context_resolution",
                    CreatedAt = now
                });
            }

            // =====================================
            // EXTENDED → MEMORY WRITE (ENUM'A %100 UYUMLU)
            // =====================================
            if (aiUxState == AIUxState.Extended)
            {
                AIContextKey contextKey =
                    match.Status == "Live"
                        ? AIContextKey.LiveMatchExtended
                        : AIContextKey.PreMatchExtended;

                _lastExtendedContextKeyRepository.Upsert(new LastExtendedContextKey
                {
                    MatchId = match.Id,
                    ContextKey = contextKey,
                    ExtendedAt = now
                });
            }

            // =====================================
            // AI DECISION TRACE
            // =====================================
            _aiDecisionTraceWriter.Write(new AIDecisionTrace
            {
                MatchId = match.Id,
                ConfidenceScore = aiUxState switch
                {
                    AIUxState.Extended => 0.65,
                    AIUxState.Short => 0.40,
                    _ => 0.25
                },
                GuardrailDecision =
                    aiUxState == AIUxState.Silent || aiUxState == AIUxState.SelfRetracted
                        ? "AI_WITHHELD_CONTEXT"
                        : "AI_CONTEXT_ALLOWED",
                AiBehaviorState = aiUxState.ToString(),
                MemoryDecayApplied = aiUxState == AIUxState.SelfRetracted,
                CreatedAt = now
            });

            // =====================================
            // RESPONSE
            // =====================================
            var homeName = _teamReadRepository.GetById(match.HomeTeamId)?.Name ?? string.Empty;
            var awayName = _teamReadRepository.GetById(match.AwayTeamId)?.Name ?? string.Empty;

            var sapma = _sapmaMotor.CalculateForListItem(new MatchListItemDto
            {
                MatchId = match.Id,
                HomeTeam = homeName,
                AwayTeam = awayName,
                StartTime = match.MatchDate,
                Status = match.Status
            });

            return new
            {
                Match = new
                {
                    match.Id,
                    match.MatchDate,
                    match.Status,
                    match.HomeTeamId,
                    match.AwayTeamId
                },

                Sapma = new
                {
                    sapma.OynanmaSkoru,
                    sapma.GucSkoru,
                    sapma.Sapma,
                    sapma.OynanmaYonu,
                    sapma.GercekGucYonu,
                    sapma.OynanmaFreshness,
                    sapma.OynanmaAgeSeconds,
                    sapma.AnalysisMuted,
                    sapma.SapmaBolgesi,
                    sapma.SessizMi,
                    sapma.SapmaMetni
                },

                AI = new
                {
                    State = aiUxState.ToString(),

                    Summary = aiUxState switch
                    {
                        AIUxState.Extended =>
                            "Maç bağlamı ve tempo analiz edilebilir seviyeye ulaşmıştır.",

                        AIUxState.Short =>
                            "Maç öncesi veri oluşuyor. Şu an ölçüm sınırlı.",

                        AIUxState.SelfRetracted =>
                            "AI, yakın zamanda yapılan değerlendirme nedeniyle bu aşamada geri çekilmeyi tercih etmiştir.",

                        _ =>
                            "Bu maç için AI şu aşamada yönlendirici bir analiz sunmamayı tercih etmiştir."
                    }
                },

                UserProtection = new
                {
                    ResponsibilityNote = AIFixedTexts.ResponsibilityNote,
                    DecisionIsYours = true
                }
            };
        }
    }
}
