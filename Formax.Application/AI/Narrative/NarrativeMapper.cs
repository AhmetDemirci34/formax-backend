using Formax.Application.DTOs.AI;

namespace Formax.Application.AI.Narrative
{
    public static class NarrativeMapper
    {
        public static AINarrativeDto ToDto(NarrativeResult result)
        {
            return new AINarrativeDto
            {
                IsSilent = result.IsSilent,
                Title = result.Title,
                Body = result.Body,
                SilentReason = result.SilentReason
            };
        }
    }
}
