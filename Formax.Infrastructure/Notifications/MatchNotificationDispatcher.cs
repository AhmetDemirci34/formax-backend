using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Notifications
{
    /// <summary>Bkz. <see cref="IMatchNotificationDispatcher"/>.</summary>
    public sealed class MatchNotificationDispatcher : IMatchNotificationDispatcher
    {
        private readonly FormaxDbContext _db;
        private readonly INotificationService _delivery;
        private readonly ILogger<MatchNotificationDispatcher> _log;

        public MatchNotificationDispatcher(
            FormaxDbContext db, INotificationService delivery, ILogger<MatchNotificationDispatcher> log)
        {
            _db = db;
            _delivery = delivery;
            _log = log;
        }

        public async Task<MatchNotificationDispatchResult> DispatchAsync(
            MatchNotificationRequest request, CancellationToken ct = default)
        {
            // Yalnız HÂLÂ aktif takip edenler (takibi bırakan IsActive=false'tur).
            var followers = await _db.UserMatchFollows.AsNoTracking()
                .Where(f => f.MatchId == request.MatchId && f.IsActive)
                .Select(f => f.UserId)
                .Distinct()
                .ToListAsync(ct);
            if (followers.Count == 0) return new(0, 0, 0, 0);

            var prefKey = MatchNotificationTypes.MatchPreferenceKey(request.MatchId);
            var muted = new HashSet<int>(await _db.UserNotificationPreferences.AsNoTracking()
                .Where(p => p.PrefKey == prefKey && !p.Enabled && followers.Contains(p.UserId))
                .Select(p => p.UserId)
                .ToListAsync(ct));

            int created = 0, duplicate = 0, byPref = 0;
            foreach (var userId in followers)
            {
                if (muted.Contains(userId)) { byPref++; continue; }

                var key = request.IdempotencyKeyForUser(userId);
                if (await _db.UserNotifications.AsNoTracking().AnyAsync(n => n.IdempotencyKey == key, ct))
                {
                    duplicate++;
                    continue;
                }

                var row = new UserNotification
                {
                    UserId = userId,
                    MatchId = request.MatchId,
                    Title = request.Title,
                    Message = request.Message,
                    IsRead = false,
                    CreatedAt = request.NowUtc,
                    EventType = request.EventType,
                    Category = NotificationCategory.Match,
                    LeagueId = request.LeagueId,
                    TargetType = NotificationTargetType.Match,
                    TargetId = request.MatchId,
                    NotificationType = request.NotificationType,
                    Route = MatchNotificationTypes.MatchRoute(request.MatchId),
                    IdempotencyKey = key
                };
                _db.UserNotifications.Add(row);
                try
                {
                    await _db.SaveChangesAsync(ct);
                    created++;
                }
                catch (DbUpdateException ex)
                {
                    // Eşzamanlı ikinci örnek aynı anahtarı yazdıysa UNIQUE indeks reddeder: tekrar yok.
                    _db.Entry(row).State = EntityState.Detached;
                    duplicate++;
                    _log.LogInformation("[NOTIFY] {Key} zaten var (eşzamanlı yazım reddedildi): {Msg}",
                        key, ex.InnerException?.Message ?? ex.Message);
                }
            }

            if (created > 0)
            {
                try { await _delivery.NotifyAsync(request.MatchId, request.Title, request.Message); }
                catch (Exception ex)
                {
                    // Push/webhook teslimi uygulama içi bildirimi GERİ ALMAZ.
                    _log.LogWarning(ex, "[NOTIFY] teslim kanalı başarısız: maç {MatchId}", request.MatchId);
                }
            }

            _log.LogInformation("[NOTIFY] {Type} maç {MatchId}: takipçi {F}, yazılan {C}, tekrar {D}, tercih kapalı {P}",
                request.NotificationType, request.MatchId, followers.Count, created, duplicate, byPref);
            return new(followers.Count, created, duplicate, byPref);
        }
    }
}
