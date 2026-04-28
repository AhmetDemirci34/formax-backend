using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface INotificationService
    {
        Task NotifyAsync(
            int matchId,
            string title,
            string message);
    }
}
