namespace Formax.Application.DTOs.Home
{
    public sealed class HomeLiveSignalMatchDto
    {
        public int MatchId { get; init; }
        public HomeTeamsDto Teams { get; init; } = new();
        public string League { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public LiveSignalDto Tempo { get; init; } = new();
        public LiveSignalDto Sertlik { get; init; } = new();
        public LiveSignalDto Momentum { get; init; } = new();
        public LiveSignalDto KritikKirilma { get; init; } = new();
        public string Note { get; init; } = string.Empty;
    }

    public sealed class LiveSignalDto
    {
        public int Value { get; init; }
        public string Level { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }
}
