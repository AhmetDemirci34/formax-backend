namespace Formax.Application.DTOs.Admin
{
    public class AdminAiTimelineDto
    {
        public AdminAiTimelineBucketDto Today { get; set; } = new();
        public AdminAiTimelineBucketDto Last7Days { get; set; } = new();
        public AdminAiTimelineBucketDto Last30Days { get; set; } = new();
    }
}
