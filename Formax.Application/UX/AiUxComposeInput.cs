using Formax.Application.AI.Guardrails;
using Formax.Application.AI.Narrative;

namespace Formax.Application.UX;

public class AiUxComposeInput
{
    public AiSpeakDecisionInput SpeakDecisionInput { get; init; } = default!;
    public NarrativeContext NarrativeContext { get; init; } = default!;

    public bool WorldUpdated { get; init; }
    public DateTime? WorldUpdatedAt { get; init; }
    public bool WorldShiftDetected { get; init; }
    public bool IsRepeatRequest { get; set; }
    public int MatchId { get; set; }
}
