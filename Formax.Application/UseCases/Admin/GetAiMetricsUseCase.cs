using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Admin;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Admin
{
    public class GetAiMetricsUseCase
    {
        private readonly IAiSpeakTelemetryRepository _telemetryRepository;

        public GetAiMetricsUseCase(
            IAiSpeakTelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<List<AiMetricsByAccessLevelDto>> ExecuteAsync()
        {
            var all = await _telemetryRepository.GetAllAsync();

            return all
                .GroupBy(x => x.AccessLevel)
                .Select(g => new AiMetricsByAccessLevelDto
                {
                    AccessLevel = g.Key.ToString(), // 🔒 ENUM → STRING
                    TotalRequests = g.Count(),
                    CanSpeakCount = g.Count(x => x.CanSpeak),
                    SilentCount = g.Count(x => !x.CanSpeak)
                })
    .ToList();
        }
    }
}
