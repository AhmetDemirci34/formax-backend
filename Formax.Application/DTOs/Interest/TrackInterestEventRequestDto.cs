namespace Formax.Application.DTOs.Interest
{
    public sealed class TrackInterestEventRequestDto
    {
        public string EventType { get; set; } = string.Empty;
        public int? MatchId { get; set; }
        public int? TeamId { get; set; }
        public string? LeagueName { get; set; }
    }
}
