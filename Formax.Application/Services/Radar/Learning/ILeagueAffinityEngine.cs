using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.4) — PURE engine. Projects the league dimension of a user's
    /// interest profile into a first-class league affinity. No EF, no repository, no
    /// DbContext, no JSON parse, no persistence — input in, output out. Deterministic.
    /// </summary>
    public interface ILeagueAffinityEngine
    {
        LeagueAffinityDto Compute(UserInterestProfileDto profile);
    }
}
