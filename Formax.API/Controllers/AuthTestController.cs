using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/auth-test")]
    public class AuthTestController : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
            var isPremium = User.FindFirstValue("isPremium");

            return Ok(new
            {
                userId,
                email,
                isPremium
            });
        }
    }
}
