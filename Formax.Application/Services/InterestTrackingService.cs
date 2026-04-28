using Formax.Application.DTOs;
using Formax.Application.Services.Discovery;


namespace Formax.Application.Services
{
    public interface IInterestTrackingService
    {
        Task TrackAsync(InterestEventDto dto);
    }

    public class InterestTrackingService : IInterestTrackingService
    {
        private readonly DiscoverySessionMemoryService _sessionMemory;
        private readonly IBanditRewardService _bandit;

        public InterestTrackingService(
            DiscoverySessionMemoryService sessionMemory,
            IBanditRewardService bandit)
        {
            _sessionMemory = sessionMemory;
            _bandit = bandit;
        }

        public async Task TrackAsync(InterestEventDto dto)
        {
           

            // reward hesapla
            var reward = _bandit.CalculateReward(dto.EventType);

            // şimdilik sadece log
       
        }
    }
}
