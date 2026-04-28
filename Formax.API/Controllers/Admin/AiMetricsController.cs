using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Formax.Infrastructure.Repositories;
using Formax.Domain.States;
using Formax.Application.DTOs.Admin;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/ai/metrics")]
    public class AiMetricsController : ControllerBase
    {
        private readonly AiStateMetricsReadRepository _metricsRepository;

        public AiMetricsController(AiStateMetricsReadRepository metricsRepository)
        {
            _metricsRepository = metricsRepository;
        }

        // ---------------------------------------
        // EXISTING METRICS (range based)
        // ---------------------------------------
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? range)
        {
            DateTime? sinceUtc = range switch
            {
                "today" => DateTime.UtcNow.Date,
                "7d" => DateTime.UtcNow.AddDays(-7),
                "30d" => DateTime.UtcNow.AddDays(-30),
                _ => null
            };

            int total;
            int extended;
            int shortState;
            int silent;
            int selfRetracted;

            if (sinceUtc.HasValue)
            {
                total = await _metricsRepository.CountTotalSinceAsync(sinceUtc.Value);
                extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, sinceUtc.Value);
                shortState = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, sinceUtc.Value);
                silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, sinceUtc.Value);
                selfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, sinceUtc.Value);
            }
            else
            {
                total = await _metricsRepository.TotalAsync();
                extended = await _metricsRepository.CountByStateAsync(AIUxState.Extended);
                shortState = await _metricsRepository.CountByStateAsync(AIUxState.Short);
                silent = await _metricsRepository.CountByStateAsync(AIUxState.Silent);
                selfRetracted = await _metricsRepository.CountByStateAsync(AIUxState.SelfRetracted);
            }

            return Ok(new
            {
                range = range ?? "all",
                total,
                states = new
                {
                    extended,
                    shortState,
                    silent,
                    selfRetracted
                }
            });
        }

        // ---------------------------------------
        // SPRINT-11 — DASHBOARD TIMELINE (DTO)
        // ---------------------------------------
        [HttpGet("timeline")]
        public async Task<IActionResult> GetTimeline()
        {
            var now = DateTime.UtcNow;

            var today = now.Date;
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            var dto = new AdminAiTimelineDto
            {
                Today = new AdminAiTimelineBucketDto
                {
                    Total = await _metricsRepository.CountTotalSinceAsync(today),
                    Extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, today),
                    Short = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, today),
                    Silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, today),
                    SelfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, today)
                },
                Last7Days = new AdminAiTimelineBucketDto
                {
                    Total = await _metricsRepository.CountTotalSinceAsync(last7Days),
                    Extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, last7Days),
                    Short = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, last7Days),
                    Silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, last7Days),
                    SelfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, last7Days)
                },
                Last30Days = new AdminAiTimelineBucketDto
                {
                    Total = await _metricsRepository.CountTotalSinceAsync(last30Days),
                    Extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, last30Days),
                    Short = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, last30Days),
                    Silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, last30Days),
                    SelfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, last30Days)
                }
            };

            return Ok(dto);
        }

        // ---------------------------------------
        // 🔥 SPRINT-12 — INVESTOR SNAPSHOT
        // ---------------------------------------
        [HttpGet("investor-snapshot")]
        public async Task<IActionResult> GetInvestorSnapshot()
        {
            var since = DateTime.UtcNow.AddDays(-30);

            var total = await _metricsRepository.CountTotalSinceAsync(since);

            var extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, since);
            var silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, since);
            var shortState = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, since);
            var selfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, since);

            var snapshot = new
            {
                period = "Last 30 Days",
                totalDecisions = total,
                behavior = new
                {
                    extended,
                    silent,
                    shortState,
                    selfRetracted
                },
                insights = new[]
                {
                    extended > silent
                        ? "AI çoğunlukla konuşmayı tercih etti."
                        : "AI çoğunlukla susmayı tercih etti.",

                    selfRetracted > 0
                        ? "AI bazı durumlarda kendini geri çekti."
                        : "AI çelişkili sinyal algılamadı."
                }
            };

            return Ok(snapshot);
        }
    }
}
