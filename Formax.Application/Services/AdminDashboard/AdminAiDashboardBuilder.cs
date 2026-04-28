using System;
using System.Threading.Tasks;
using Formax.Domain.States;
using Formax.Application.DTOs.Admin;
using Formax.Application.Interfaces.Repositories;

namespace Formax.Application.Services.AdminDashboard
{
    public class AdminAiDashboardBuilder
    {
        private readonly IAiStateMetricsReadRepository _metricsRepository;

        public AdminAiDashboardBuilder(
            IAiStateMetricsReadRepository metricsRepository)
        {
            _metricsRepository = metricsRepository;
        }

        public async Task<AdminAiTimelineDto> BuildAsync()
        {
            var now = DateTime.UtcNow;

            return new AdminAiTimelineDto
            {
                Today = await BuildBucket(now.Date),
                Last7Days = await BuildBucket(now.AddDays(-7)),
                Last30Days = await BuildBucket(now.AddDays(-30))
            };
        }

        private async Task<AdminAiTimelineBucketDto> BuildBucket(DateTime sinceUtc)
        {
            return new AdminAiTimelineBucketDto
            {
                Total = await _metricsRepository.CountTotalSinceAsync(sinceUtc),
                Extended = await _metricsRepository.CountByStateSinceAsync(AIUxState.Extended, sinceUtc),
                Short = await _metricsRepository.CountByStateSinceAsync(AIUxState.Short, sinceUtc),
                Silent = await _metricsRepository.CountByStateSinceAsync(AIUxState.Silent, sinceUtc),
                SelfRetracted = await _metricsRepository.CountByStateSinceAsync(AIUxState.SelfRetracted, sinceUtc)
            };
        }
    }
}
