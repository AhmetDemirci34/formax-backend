using System;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IInterestEngine
    {
        Task ProcessMatchEventAsync(
            int userId,
            int matchId,
            string eventType,
            string teamName,
            DateTime occurredAtUtc);
    }
}