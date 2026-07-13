using System.Collections.Generic;
using Formax.Application.DTOs.Radar;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.2) — PURE interest engine. Given a user's events plus plain
    /// match and signal context, produces the interest profile. No EF, no repository, no
    /// DbContext, no persistence, no I/O. Deterministic.
    /// </summary>
    public interface IUserInterestEngine
    {
        UserInterestProfileDto Compute(
            int userId,
            IReadOnlyList<LearningEvent> events,
            IReadOnlyDictionary<int, InterestMatchContext> matchContext,
            IReadOnlyDictionary<int, IReadOnlyList<InterestSignal>> signalContext);
    }
}
