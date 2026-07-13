using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.1) — records normalized learning events from user behaviour.
    /// Collection only: no scoring, no profile, no affinity, no ranking effect.
    /// </summary>
    public interface ILearningEventService
    {
        Task RecordAsync(int userId, int matchId, LearningEventType type,
            double? value = null, string? source = null, CancellationToken ct = default);

        Task RecordSwipeAsync(int userId, int matchId, CancellationToken ct = default);
        Task RecordViewAsync(int userId, int matchId, double? viewDurationMs = null, CancellationToken ct = default);
        Task RecordDetailOpenAsync(int userId, int matchId, CancellationToken ct = default);
        Task RecordDetailReturnAsync(int userId, int matchId, double? detailDurationMs = null, CancellationToken ct = default);
        Task RecordFollowAsync(int userId, int matchId, CancellationToken ct = default);
        Task RecordUnfollowAsync(int userId, int matchId, CancellationToken ct = default);
    }
}
