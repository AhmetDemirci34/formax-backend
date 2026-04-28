using System;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IUserInterestTrackingService
    {
        Task TrackAsync(
            int userId,
            string eventType,
            int? matchId,
            int? teamId,
            string? leagueName,
            DateTime utcNow);
    }
}
