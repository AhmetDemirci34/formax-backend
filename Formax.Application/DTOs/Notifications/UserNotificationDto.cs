using System;

namespace Formax.Application.DTOs.Notifications
{
    public class UserNotificationDto
    {
        public int Id { get; set; }
        public int MatchId { get; set; }

        public required string Title { get; set; }
        public required string Message { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
