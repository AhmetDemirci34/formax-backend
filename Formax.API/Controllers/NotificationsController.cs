using Formax.Application.UseCases.Notifications;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly GetUserNotificationsUseCase _useCase;

        public NotificationsController(GetUserNotificationsUseCase useCase)
        {
            _useCase = useCase;
        }

        // TEMP: userId param (JWT sonra)
        [HttpGet("me")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int userId)
        {
            var result = await _useCase.ExecuteAsync(userId);
            return Ok(result);
        }
    }
}
