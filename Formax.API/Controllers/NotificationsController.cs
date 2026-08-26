using Formax.Application.Interfaces;
using Formax.Application.UseCases.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

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
