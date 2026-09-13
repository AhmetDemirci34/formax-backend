using Formax.Application.Interfaces;
using Formax.Application.UseCases.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly GetUserNotificationsUseCase _useCase;
        private readonly IUserNotificationRepository _repository;

        public NotificationsController(
            GetUserNotificationsUseCase useCase,
            IUserNotificationRepository repository)
        {
            _useCase = useCase;
            _repository = repository;
        }

        private int GetUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub")
                      ?? User?.FindFirstValue("userId");
            return int.TryParse(raw, out var id) ? id : 0;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var result = await _useCase.ExecuteAsync(userId);
            return Ok(result);
        }

        [HttpGet("me/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var count = await _repository.CountUnreadAsync(userId);
            return Ok(new { count });
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            await _repository.MarkAsReadAsync(id);
            return NoContent();
        }

        // ── BİLDİRİM TERCİHLERİ ─────────────────────────────────────────────────
        // İçerik bazlı aç/kapat ("match:123" …). Satır yoksa tercih AÇIK sayılır. Bildirim
        // üreten job'lar (kadro, kritik gelişme) göndermeden önce bu tabloya bakar.

        private static readonly System.Text.RegularExpressions.Regex PrefKeyRx =
            new(@"^(match|team|league|formax):[A-Za-z0-9\-]{1,60}$");

        public sealed record PreferenceDto(string Key, bool Enabled);

        [HttpGet("me/preferences")]
        public async Task<IActionResult> GetPreferences(
            [FromServices] FormaxDbContext db, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            var rows = await db.UserNotificationPreferences.AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new PreferenceDto(p.PrefKey, p.Enabled))
                .ToListAsync(ct);
            return Ok(rows);
        }

        [HttpPut("me/preferences")]
        public async Task<IActionResult> SetPreference(
            [FromBody] PreferenceDto body,
            [FromServices] FormaxDbContext db, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            if (body == null || !PrefKeyRx.IsMatch(body.Key ?? string.Empty))
                return BadRequest(new { error = "Geçersiz tercih anahtarı" });

            var row = await db.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.PrefKey == body.Key, ct);
            if (row == null)
            {
                db.UserNotificationPreferences.Add(new Formax.Domain.Entities.UserNotificationPreference
                {
                    UserId = userId, PrefKey = body.Key!, Enabled = body.Enabled, UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                row.Enabled = body.Enabled;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return Ok(new PreferenceDto(body.Key!, body.Enabled));
        }

        /// <summary>Kullanıcının tüm okunmamış bildirimlerini tek işlemde okundu yapar.</summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var updated = await _repository.MarkAllAsReadAsync(userId);
            return Ok(new { updated });
        }
    }
}
