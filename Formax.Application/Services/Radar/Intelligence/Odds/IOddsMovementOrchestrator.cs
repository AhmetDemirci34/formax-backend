using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.2) — ingests a new odds reading, compares it with the
    /// previous reading for the same match, and persists a movement when a previous
    /// reading exists. The first reading produces no movement; each subsequent reading
    /// produces one (current vs previous).
    /// </summary>
    public interface IOddsMovementOrchestrator
    {
        /// <summary>Persist the new reading and, if a previous reading exists, the
        /// derived movement. Returns the movement, or null for the first reading.</summary>
        Task<OddsMovementSnapshot?> IngestAsync(OddsSnapshot newReading, CancellationToken ct = default);
    }
}
