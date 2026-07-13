using Formax.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IUserNotificationRepository
    {
        Task<List<UserNotification>> GetByUserAsync(int userId);
        Task<int> CountUnreadAsync(int userId);
        Task AddAsync(UserNotification notification);
        Task MarkAsReadAsync(int notificationId);
    }
}


