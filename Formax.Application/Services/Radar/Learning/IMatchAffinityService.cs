using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — orchestration. Reads the user's interest profile and the
    /// match/snapshot, parses SignalsJson, builds context, and runs the PURE engine.
    /// In-memory; persists nothing. Not wired into ranking/recommendation.
    /// </summary>
    public interface IMatchAffinityService
    {
        Task<MatchAffinityDto> GetAffinityAsync(int userId, int matchId, CancellationToken ct = default);
    }
}
