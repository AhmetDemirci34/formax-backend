using System;

namespace Formax.Domain.Entities
{
    public class UserInterestEvent
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public InterestEventType EventType { get; set; }
        public int? MatchId { get; set; }
        public int? TeamId { get; set; }
        public string? LeagueName { get; set; }
        public int Weight { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
