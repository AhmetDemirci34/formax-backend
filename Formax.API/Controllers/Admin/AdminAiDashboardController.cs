using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Formax.Application.Services.AdminDashboard;
using Formax.Application.Services.AI;
using Formax.Application.DTOs.Admin;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/dashboard")]
    public class AdminAiDashboardController : ControllerBase
    {
        private readonly AdminAiDashboardAssembler _assembler;
        private readonly AnalyticsQueryService _analyticsQueryService;

        public AdminAiDashboardController(
            AdminAiDashboardAssembler assembler,
            AnalyticsQueryService analyticsQueryService)
        {
            _assembler = assembler;
            _analyticsQueryService = analyticsQueryService;
        }

        /// <summary>
        /// Admin AI Dashboard – Timeline + Insights
        /// (Read-only, demo & monitoring amaçlı)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(AdminDashboardViewDto), 200)]
        public async Task<IActionResult> Get()
        {
            var result = await _assembler.BuildAsync();
            return Ok(result);
        }

        /// <summary>
        /// GLOBAL ANALYTICS (CTR / Skip / Follow)
        /// </summary>
        [HttpGet("analytics/global")]
        public async Task<IActionResult> GetGlobalAnalytics()
        {
            var result = await _analyticsQueryService.GetGlobalMetricsAsync();
            return Ok(result);
        }

        /// <summary>
        /// TOP MATCH PERFORMANCE
        /// </summary>
        [HttpGet("analytics/top-matches")]
        public async Task<IActionResult> GetTopMatches([FromQuery] int top = 20)
        {
            var result = await _analyticsQueryService.GetTopMatchesAsync(top);
            return Ok(result);
        }
    }
}