namespace Formax.API.Contracts.Live
{
    public sealed class EmitMatchEventRequest
    {
        public string EventType { get; set; } = default!;
        public int Minute { get; set; }
        public string TeamName { get; set; } = default!;
        public string? PlayerName { get; set; }
        public string Description { get; set; } = default!;
    }
}
