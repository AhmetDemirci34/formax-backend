using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// AI anlatı ısıtma (prewarm) yönetimi. Üretim tetikleyicisi
    /// <see cref="NarrativePrewarmJob"/>'ın 20 dk'lık döngüsüdür; bu uç yalnız manuel
    /// doğrulama/ilk doldurma içindir.
    ///
    /// AI Maç Analizi'nin kendisine dokunmaz — aynı zincirin çalışma zamanını öne alır.
    /// </summary>
    [ApiController]
    [Route("admin/narrative")]
    public class AdminNarrativeController : ControllerBase
    {
        private readonly NarrativePrewarmJob _job;

        public AdminNarrativeController(NarrativePrewarmJob job) => _job = job;

        [HttpPost("prewarm")]
        public async Task<IActionResult> Prewarm(CancellationToken ct)
        {
            var warmed = await _job.RunCycleAsync(ct);
            return Ok(new { message = "narrative prewarm cycle ran", warmed });
        }
    }
}
