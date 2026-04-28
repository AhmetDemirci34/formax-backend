namespace Formax.Application.DTOs.Live
{
    public class UserNotificationDto
    {
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string? Sound { get; set; }
        public bool IsSoundEnabled { get; set; }
    }
}
