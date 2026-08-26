using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Fikstür senkronizasyonunun geçmiş pencere tetiği (teşhis / şema sonrası geri doldurma).
    /// Otomatik tetik FixtureSyncJob'un kendi döngüsüdür (dün → +7 gün); burası yalnız
    /// eski tamamlanmış maçların yeni alanlarla (ör. ilk yarı skoru) güncellenmesi içindir.
    /// Maliyet: pencere gün sayısı kadar api-football isteği.
    /// </summary>
    [ApiController]
    [Route("admin/fixtures")]
    public class AdminFixturesController : ControllerBase
    {
        private readonly FixtureSyncJob _job;

        public AdminFixturesController(FixtureSyncJob job) => _job = job;

        [HttpPost("backfill")]
        public async Task<IActionResult> Backfill(
            [FromQuery] string from,
            [FromQuery] string to,
            CancellationToken ct)
        {
            if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
                return BadRequest(new { error = "from/to must be yyyy-MM-dd" });

            if (toDate < fromDate) return BadRequest(new { error = "to < from" });

            var days = (toDate.Date - fromDate.Date).Days + 1;
            if (days > 45) return BadRequest(new { error = "window too large (max 45 days)", days });

            await _job.RunBackfillAsync(fromDate, toDate, ct);
            return Ok(new { from = fromDate.ToString("yyyy-MM-dd"), to = toDate.ToString("yyyy-MM-dd"), days });
        }
    }
}
