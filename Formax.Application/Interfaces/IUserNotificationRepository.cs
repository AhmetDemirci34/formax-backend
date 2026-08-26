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
        /// <summary>Kullanıcının tüm okunmamış bildirimlerini tek işlemde okundu yapar; etkilenen satır sayısını döner.</summary>
        Task<int> MarkAllAsReadAsync(int userId);
    }
}


