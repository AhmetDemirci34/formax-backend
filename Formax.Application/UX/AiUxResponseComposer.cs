using Formax.Application.AI.Guardrails;
using Formax.Application.AI.Narrative;
using Formax.Application.AI.UX;
using Formax.Application.Services;
using Formax.Domain.States;

namespace Formax.Application.UX;

public sealed class AiUxResponseComposer
{
    private readonly AiSpeakDecisionService _speakDecisionService;
    private readonly IAINarrativeBuilder _narrativeBuilder;
    private readonly WorldExpectationService _worldExpectationService;

    public AiUxResponseComposer(
        AiSpeakDecisionService speakDecisionService,
        IAINarrativeBuilder narrativeBuilder,
        WorldExpectationService worldExpectationService)
    {
        _speakDecisionService = speakDecisionService;
        _narrativeBuilder = narrativeBuilder;
        _worldExpectationService = worldExpectationService;
    }

    public AiUxResponseDto Compose(AiUxComposeInput input)
    {
        var decision = _speakDecisionService.Decide(input.SpeakDecisionInput);

        // 🔕 Guard: konuşamaz
        if (!decision.CanSpeak)
        {
            return new AiUxResponseDto
            {
                IsSilent = true,
                SilenceReasonKey = decision.SilenceReason?.ToString(),
                SilenceMessage =
                    SilenceReasonUxMap.GetMessage(
                        decision.SilenceReason?.ToString()),

                UxState = AIUxState.Silent.ToString(),

                Silence = new SilenceStatus
                {
                    Active = true,
                    Reason = decision.SilenceReason
                },
                Permissions = new PermissionStatus
                {
                    CanAskAgain = false,
                    CanExtend = false,
                    RequiresPremium = false
                }
            };
        }

        // 🔑 Bu aşamada UX state
        var uxState = AIUxState.Short;

        // 🔑 Geçici ama zorunlu context key
        var contextKey = AIContextKey.PreMatchSummary;

        // 🧠 Narrative üret
        NarrativeResult narrative =
            _narrativeBuilder.Build(
                input.NarrativeContext,
                uxState,
                contextKey
            );

        // 🔕 Narrative kendi kendine sessiz kaldıysa
        if (narrative.IsSilent)
        {
            return new AiUxResponseDto
            {
                IsSilent = true,
                SilenceReasonKey = narrative.SilentReason?.ToString(),
                SilenceMessage =
                    SilenceReasonUxMap.GetMessage(
                        narrative.SilentReason?.ToString()),

                UxState = AIUxState.Silent.ToString(),

                Silence = new SilenceStatus
                {
                    Active = true,
                    Reason = narrative.SilentReason
                },
                Permissions = new PermissionStatus
                {
                    CanAskAgain = false,
                    CanExtend = false,
                    RequiresPremium = false
                }
            };
        }

        // ✍️ Normal çıktı
        return new AiUxResponseDto
        {
            IsSilent = false,
            SilenceReasonKey = null,
            SilenceMessage = null,

            UxState = uxState.ToString(),

            WorldExpectation =
                _worldExpectationService.GetForMatch(input.MatchId),

            World = new WorldStatus
            {
                Updated = input.WorldUpdated,
                UpdatedAt = input.WorldUpdatedAt,
                ShiftDetected = input.WorldShiftDetected
            },

            Narrative = new NarrativeStatus
            {
                Base = new NarrativeBlock
                {
                    Exists = !string.IsNullOrWhiteSpace(narrative.Body),
                    Text = narrative.Body
                }
            },

            Permissions = new PermissionStatus
            {
                CanAskAgain = true,
                CanExtend = false,
                RequiresPremium = false
            }
        };
    }
}
