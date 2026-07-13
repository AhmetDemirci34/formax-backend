using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — PURE affinity engine. Given a user's interest profile and
    /// plain match context, produces the affinity. No EF, no repository, no DbContext, no
    /// API, no JSON parse, no persistence — only inputs in, DTO out. Deterministic.
    /// </summary>
    public interface IMatchAffinityEngine
    {
        MatchAffinityDto Compute(UserInterestProfileDto profile, MatchAffinityContext match);
    }
}
