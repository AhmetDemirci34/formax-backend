using Formax.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// SÜREÇ SAĞLIĞI — son dakikaların runtime örnekleri, takılma anları ve /detail gecikme dağılımı.
    /// Salt okunur; hiçbir dış kaynağa çıkmaz.
    /// </summary>
    [ApiController]
    [Route("admin/diagnostics")]
    public sealed class AdminDiagnosticsController : ControllerBase
    {
        private readonly RuntimeHealthMonitor _monitor;
        public AdminDiagnosticsController(RuntimeHealthMonitor monitor) => _monitor = monitor;

        [HttpGet("runtime")]
        public IActionResult Runtime([FromQuery] int minutes = 15) => Ok(_monitor.Snapshot(minutes));
    }
}
