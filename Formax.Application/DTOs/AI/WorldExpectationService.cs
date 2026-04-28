using Formax.Application.DTOs.AI;

namespace Formax.Application.Services
{
    public sealed class WorldExpectationService
    {
        public WorldExpectationDto GetForMatch(int matchId)
        {
            return new WorldExpectationDto
            {
                ConfidenceLevel = 62,
                Summary = "Genel beklenti maçın dengede geçeceği yönünde.",
                IsStale = false,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }
}
