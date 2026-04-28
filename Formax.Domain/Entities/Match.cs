namespace Formax.Domain.Entities
{
    public class Match
    {
        public int Id { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public DateTime MatchDate { get; set; }

        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        public string? MatchMinute { get; set; }
        public string Status { get; set; } = "NotStarted";

        public string League { get; set; } = string.Empty;
        public int LeagueId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? LastEmittedEventType { get; set; }

        // 🔥 GERİ EKLENDİ
        public Match CloneForComparison()
        {
            return (Match)this.MemberwiseClone();
        }

        // 🔥 NEW NAV
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }
    }
}