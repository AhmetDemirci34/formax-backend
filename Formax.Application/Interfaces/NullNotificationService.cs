using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Services
{
    public class NullNotificationService : INotificationService
    {
        public Task NotifyAsync(
            int matchId,
            string title,
            string message)
        {
            // NO-OP: deliberately does nothing
            return Task.CompletedTask;
        }
    }
}
