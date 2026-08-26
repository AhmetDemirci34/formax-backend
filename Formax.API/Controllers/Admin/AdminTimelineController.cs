using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Timeline Operations & Coverage Dashboard — salt-okunur operasyonel görünüm.
    /// Tüm değerler gerçek veriden (Teams/Matches + metrics/telemetry); cache'li; Timeline'ı etkilemez.
    /// </summary>
    [ApiController]
    [Route("admin/timeline")]
    public class AdminTimelineController : ControllerBase
    {
        private readonly ITimelineCoverageService _coverage;

        public AdminTimelineController(ITimelineCoverageService coverage)
        {
            _coverage = coverage;
        }

        /// <summary>Global Timeline Coverage Dashboard.</summary>
        [HttpGet("dashboard")]
        public IActionResult Dashboard() => Ok(_coverage.GetDashboard());

        /// <summary>Lig bazında Timeline raporu. ?top=N ile ilk N lig.</summary>
        [HttpGet("leagues")]
        public IActionResult Leagues([FromQuery] int? top)
        {
            var all = _coverage.GetLeagues();
            if (top is > 0) return Ok(new { count = all.Count, leagues = all.Take(top.Value) });
            return Ok(new { count = all.Count, leagues = all });
        }

        /// <summary>API Quota Intelligence — gerçek ölçüm + kapasite.</summary>
        [HttpGet("quota")]
        public async Task<IActionResult> Quota(CancellationToken ct)
            => Ok(await _coverage.GetQuotaReportAsync(ct));

        /// <summary>Cold Start önceliklendirme önizlemesi (API çağrısı YOK) — kanıt için.</summary>
        [HttpGet("coldstart-preview")]
        public IActionResult ColdStartPreview([FromQuery] int count = 20)
            => Ok(_coverage.GetColdStartPreview(count));
    }
}
