using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.4) — orchestration. Reads the user's interest profile (R.14.2)
    /// and runs the PURE league affinity engine. In-memory; persists nothing. Not wired
    /// into ranking/recommendation/feed.
    /// </summary>
    public interface ILeagueAffinityService
    {
        Task<LeagueAffinityDto> GetAsync(int userId, CancellationToken ct = default);
    }
}
