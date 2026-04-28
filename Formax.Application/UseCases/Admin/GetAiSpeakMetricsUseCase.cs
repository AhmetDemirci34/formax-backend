using Formax.Application.DTOs.Admin;
using Formax.Application.Services.AdminDashboard;

namespace Formax.Application.UseCases.Admin
{
    public class GetAiSpeakMetricsUseCase
    {
        private readonly AiSpeakMetricsCalculator _calculator;

        public GetAiSpeakMetricsUseCase(
            AiSpeakMetricsCalculator calculator)
        {
            _calculator = calculator;
        }

        public AiSpeakMetricsDto Execute()
        {
            return _calculator.Calculate();
        }
    }
}
