using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.2) — orchestration. Reads the user's LearningEvents and the
    /// match/signal context from existing repositories, then runs the PURE engine.
    /// Computes in-memory; persists nothing this sprint.
    /// </summary>
    public interface IUserInterestProfileService
    {
        Task<UserInterestProfileDto> GetProfileAsync(int userId, CancellationToken ct = default);
    }
}
