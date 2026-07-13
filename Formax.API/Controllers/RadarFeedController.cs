using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Feed;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Radar Feed (R.13.2) — exposes Radar-fed feed insights. Read-only; no ranking
    /// algorithm, no learning, no notifications.
    /// </summary>
    [ApiController]
    [Route("api/radar/feed")]
    public class RadarFeedController : ControllerBase
    {
        private readonly IFeedInsightQueryService _queryService;

        public RadarFeedController(IFeedInsightQueryService queryService)
        {
            _queryService = queryService;
        }

        /// <summary>
        /// GET /api/radar/feed — feed insights, Hidden excluded, ImportanceScore DESC,
        /// first 50.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFeed(CancellationToken ct)
        {
            var feed = await _queryService.GetFeedAsync(50, ct);
            return Ok(feed);
        }
    }
}
