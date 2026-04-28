using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    public class UserNotificationRepository : IUserNotificationRepository
    {
        private readonly FormaxDbContext _context;

        public UserNotificationRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserNotification>> GetByUserAsync(int userId)
        {
            return await _context.UserNotifications
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.IsRead)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(UserNotification notification)
        {
            _context.UserNotifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(x => x.Id == notificationId);

            if (notification == null)
                return;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}
