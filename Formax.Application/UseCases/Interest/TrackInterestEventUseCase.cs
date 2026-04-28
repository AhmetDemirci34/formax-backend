using System;
using System.Threading.Tasks;
using Formax.Application.DTOs.Interest;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Interest
{
    public sealed class TrackInterestEventUseCase
    {
        private readonly IUserInterestTrackingService _trackingService;

        public TrackInterestEventUseCase(IUserInterestTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        public async Task ExecuteAsync(int userId, TrackInterestEventRequestDto request, DateTime utcNow)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Invalid user id.");

            if (request == null)
                throw new InvalidOperationException("Request is required.");

            await _trackingService.TrackAsync(
                userId,
                request.EventType,
                request.MatchId,
                request.TeamId,
                request.LeagueName,
                utcNow);
        }
    }
}
