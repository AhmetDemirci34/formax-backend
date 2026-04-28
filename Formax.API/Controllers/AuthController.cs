using Formax.Application.AI.Contexts;
using Formax.Application.DTOs.Auth;
using Formax.Application.Interfaces;
using Formax.Application.Services;
using Formax.Application.UseCases.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        [HttpPost("register")]
        public IActionResult Register(
            [FromServices] RegisterUserUseCase useCase,
            [FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = useCase.Execute(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPost("login")]
        public IActionResult Login(
            [FromServices] LoginUserUseCase useCase,
            [FromServices] UserExperienceTransferService transferService,
            [FromBody] LoginRequestDto request,
            [FromHeader(Name = "X-Experience-Matches")] int matchesWithAi = 0,
            [FromHeader(Name = "X-Experience-AiCount")] int aiContextCount = 0,
            [FromHeader(Name = "X-Experience-RegisterHint")] bool hasSeenHint = false,
            [FromHeader(Name = "X-Experience-LastType")] string? lastType = null
        )
        {
            try
            {
                var auth = useCase.Execute(request);

                var ctx = new UserExperienceContext
                {
                    IsRegistered = true,
                    UserId = auth.UserId,
                    MatchesWithAiCount = matchesWithAi,
                    AiContextShownCount = aiContextCount,
                    HasSeenRegisterHint = hasSeenHint,
                    LastAiInteractionType = lastType
                };

                transferService.TransferToUser(ctx);

                return Ok(auth);
            }
            catch (InvalidOperationException)
            {
                // UseCase "Invalid credentials." fırlatıyor -> 401 dönelim
                return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });
            }
        }

        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword(
            [FromServices] IUserRepository userRepo,
            [FromBody] ForgotPasswordRequestDto request)
        {
            var email = (request?.Email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email zorunlu." });

            var user = userRepo.GetByEmail(email);

            if (user == null)
                return NotFound(new { message = "Hesap bulunamadı." });

            // MVP stub: gerçek mail gönderimi yok, sadece simüle ediyoruz
            return Ok(new
            {
                message = "Şifre sıfırlama bağlantısı gönderildi."
            });
        }
    }
}