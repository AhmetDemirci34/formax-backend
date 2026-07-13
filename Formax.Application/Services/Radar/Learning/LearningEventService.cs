using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.1) — default service. Normalizes user behaviour into
    /// <see cref="LearningEvent"/> rows. Each typed helper maps to one event type. Pure
    /// collection — no scoring/profile/affinity/ranking.
    /// </summary>
    public sealed class LearningEventService : ILearningEventService
    {
        private readonly ILearningEventRepository _repository;
        private readonly ILogger<LearningEventService> _logger;

        public LearningEventService(
            ILearningEventRepository repository,
            ILogger<LearningEventService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task RecordAsync(int userId, int matchId, LearningEventType type,
            double? value = null, string? source = null, CancellationToken ct = default)
        {
            var evt = new LearningEvent
            {
                UserId = userId,
                MatchId = matchId,
                EventType = type,
                Value = value,
                Source = source ?? type.ToString().ToLowerInvariant(),
                OccurredAtUtc = DateTime.UtcNow
            };

            await _repository.AddAsync(evt, ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogDebug("[LEARNING EVENT] user={U} match={M} type={T} value={V}",
                userId, matchId, type, value);
        }

        public Task RecordSwipeAsync(int userId, int matchId, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.Swipe, source: "swipe", ct: ct);

        public Task RecordViewAsync(int userId, int matchId, double? viewDurationMs = null, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.View, viewDurationMs, "view", ct);

        public Task RecordDetailOpenAsync(int userId, int matchId, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.DetailOpen, source: "detail", ct: ct);

        public Task RecordDetailReturnAsync(int userId, int matchId, double? detailDurationMs = null, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.DetailReturn, detailDurationMs, "detail", ct);

        public Task RecordFollowAsync(int userId, int matchId, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.Follow, source: "follow", ct: ct);

        public Task RecordUnfollowAsync(int userId, int matchId, CancellationToken ct = default)
            => RecordAsync(userId, matchId, LearningEventType.Unfollow, source: "follow", ct: ct);
    }
}
