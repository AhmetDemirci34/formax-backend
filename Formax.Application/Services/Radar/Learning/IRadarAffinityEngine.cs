using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.5) — PURE engine. Projects the signal dimension of a user's
    /// interest profile into a first-class radar affinity (per-signal + grouped). No EF,
    /// no repository, no DbContext, no JSON parse, no persistence. Deterministic.
    /// </summary>
    public interface IRadarAffinityEngine
    {
        RadarAffinityDto Compute(UserInterestProfileDto profile);
    }
}
