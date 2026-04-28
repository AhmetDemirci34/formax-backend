using Formax.Domain.States;

namespace Formax.Application.AI.Narrative
{
    public interface IAINarrativeBuilder
    {
        NarrativeResult Build(
            NarrativeContext context,
            AIUxState uxState,
            AIContextKey contextKey
        );
    }
}
