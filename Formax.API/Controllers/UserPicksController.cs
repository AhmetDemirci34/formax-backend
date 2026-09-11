using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.UseCases.Picks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// "SENİN SEÇİMİN" — kullanıcının olası sonuç seçimleri.
    ///
    /// GİRİŞ ZORUNLUDUR. Seçim, kişiye ait kalıcı bir kayıttır ve cihazlar arasında
    /// aynı görünmelidir; anonim bir kimlik uydurmak (sahte UserId, yalnız
    /// localStorage) bunu sağlamaz ve kullanıcıya yanlış bir kalıcılık sözü verirdi.
    /// Giriş yapılmamışsa arayüz mevcut auth akışını gösterir.
    ///
    /// Bu uçların hiçbiri sağlayıcıya çıkmaz: yalnız FORMAX veritabanı okunur/yazılır.
    /// </summary>
    [ApiController]
    [Route("api/picks")]
    [Authorize]
    public sealed class UserPicksController : ControllerBase
    {
        private string? CurrentUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub")
                      ?? User?.FindFirstValue("userId");
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        /// <summary>
        /// Seçimi ekler veya kaldırır (aynı satıra tekrar basmak seçimi kaldırır).
        ///
        /// Reddedilme sebepleri açıkça döner — arayüz sessizce "kaydedildi" DEMEZ:
        ///  • MATCH_NOT_FOUND — maç yok
        ///  • UNKNOWN_MARKET — etiketin bilinen bir market karşılığı yok
        ///  • MATCH_ALREADY_STARTED — maç başladı; yeni seçim kabul edilmez
        /// </summary>
        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle(
            [FromBody] UserPickRequest request,
            [FromServices] UserPickSelectionUseCase useCase,
            CancellationToken ct)
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();
            if (request == null || request.MatchId <= 0)
                return BadRequest(new { error = "matchId gerekli" });

            var result = await useCase.ToggleAsync(userId, request, DateTime.UtcNow, ct);

            if (!result.Accepted)
                return Ok(new { accepted = false, reason = result.RejectionReason, selections = result.Selections });

            return Ok(new { accepted = true, selections = result.Selections });
        }

        /// <summary>Bir maçtaki mevcut seçimler — sayfa yenilendiğinde durum buradan geri gelir.</summary>
        [HttpGet("match/{matchId:int}")]
        public async Task<IActionResult> ForMatch(
            int matchId,
            [FromServices] UserPickSelectionUseCase useCase,
            CancellationToken ct)
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();
            return Ok(new { matchId, selections = await useCase.GetForMatchAsync(userId, matchId, ct) });
        }

        /// <summary>TAHMİNLERİM — kullanıcının bütün seçimleri, maç kartları hâlinde.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> Mine(
            [FromServices] GetUserPredictionsUseCase useCase,
            CancellationToken ct)
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();
            var cards = await useCase.ExecuteAsync(userId, DateTime.UtcNow, ct);
            return Ok(new { count = cards.Count, cards });
        }
    }
}
