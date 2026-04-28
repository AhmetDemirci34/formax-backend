using System.Threading.Tasks;
using Formax.Application.DTOs.Admin;

namespace Formax.Application.Services.AdminDashboard
{
    /// <summary>
    /// Admin dashboard için Timeline + Insights çıktısını birleştirir.
    /// Snapshot bilinçli olarak bu sprintte dışarıda bırakılmıştır.
    /// </summary>
    public class AdminAiDashboardAssembler
    {
        private readonly AdminAiDashboardBuilder _timelineBuilder;
        private readonly AdminAiInsightEngine _insightEngine;

        public AdminAiDashboardAssembler(
            AdminAiDashboardBuilder timelineBuilder,
            AdminAiInsightEngine insightEngine)
        {
            _timelineBuilder = timelineBuilder;
            _insightEngine = insightEngine;
        }

        public async Task<AdminDashboardViewDto> BuildAsync()
        {
            var timeline = await _timelineBuilder.BuildAsync();
            var insights = _insightEngine.BuildInsights(timeline);

            return new AdminDashboardViewDto
            {
                Timeline = timeline,
                Insights = insights
            };
        }
    }
}
