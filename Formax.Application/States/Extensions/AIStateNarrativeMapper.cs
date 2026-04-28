using Formax.Domain.States;
using Formax.Application.AI.Narrative;

namespace Formax.Application.States.Extensions
{
    public static class AIStateNarrativeMapper
    {
        public static NarrativeResult MapToSilent(
            AIUxState uxState,
            AIContextKey contextKey)
        {
            return new NarrativeResult
            {
                IsSilent = true,
                SilentReason = "AI bu durumda anlatı üretmiyor."
            };
        }
    }
}
