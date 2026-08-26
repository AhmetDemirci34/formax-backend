namespace Formax.Application.DTOs.Follow
{
    /// <summary>GET /api/follow/summary yanıtı — takip özet sayımları.</summary>
    public class FollowSummaryDto
    {
        public int TeamsCount { get; set; }
        public int MatchesCount { get; set; }
        public int LeaguesCount { get; set; }
    }
}
