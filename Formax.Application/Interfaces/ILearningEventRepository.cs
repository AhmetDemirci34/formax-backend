using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Learning (R.14.1) — persistence for normalized learning events.
    /// </summary>
    public interface ILearningEventRepository
    {
        Task AddAsync(LearningEvent learningEvent, CancellationToken ct = default);

        /// <summary>Recent events for a user, newest first (diagnostics/verification).</summary>
        Task<IReadOnlyList<LearningEvent>> GetByUserAsync(int userId, int take = 100, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
