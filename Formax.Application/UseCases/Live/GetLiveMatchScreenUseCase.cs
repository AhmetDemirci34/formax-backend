using Formax.Application.DTOs.Live;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Live
{
    public class GetLiveMatchScreenUseCase
    {
        private readonly GetLiveMatchReadingUseCase _getLiveMatchReadingUseCase;
        private readonly GetLiveMatchTimelineUseCase _getLiveMatchTimelineUseCase;

        public GetLiveMatchScreenUseCase(
            GetLiveMatchReadingUseCase getLiveMatchReadingUseCase,
            GetLiveMatchTimelineUseCase getLiveMatchTimelineUseCase)
        {
            _getLiveMatchReadingUseCase = getLiveMatchReadingUseCase;
            _getLiveMatchTimelineUseCase = getLiveMatchTimelineUseCase;
        }

        public async Task<LiveMatchScreenDto> ExecuteAsync(int matchId)
        {
            var liveReading = await _getLiveMatchReadingUseCase.ExecuteAsync(matchId);
            var timeline = await _getLiveMatchTimelineUseCase.ExecuteAsync(matchId);

            return new LiveMatchScreenDto
            {
                Live = liveReading,
                Timeline = timeline,
                IsHighRiskMatch = false // UI bağlamı, şimdilik sabit
            };
        }
    }
}
