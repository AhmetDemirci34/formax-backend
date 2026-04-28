using Formax.Domain.Subscriptions;

namespace Formax.Application.DTOs.Admin
{
    public class AiMetricsByAccessLevelDto
    {
        public string AccessLevel { get; set; } = null!;
        public int TotalRequests { get; set; }
        public int CanSpeakCount { get; set; }
        public int SilentCount { get; set; }
    }
}