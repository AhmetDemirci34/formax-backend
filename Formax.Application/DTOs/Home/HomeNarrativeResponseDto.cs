using System.Collections.Generic;

namespace Formax.Application.DTOs.Home
{
    public sealed class HomeNarrativeResponseDto
    {
        public string SummaryText { get; init; } = string.Empty;
        public HomeContentAccessDto ContentAccess { get; init; } = new();
        public IReadOnlyList<HomeNarrativeCardDto> Cards { get; init; } = new List<HomeNarrativeCardDto>();
    }

    public sealed class HomeNarrativeCardDto
    {
        public int? MatchId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Tone { get; init; } = string.Empty;
        public string Tag { get; init; } = string.Empty;
        public bool IsPremiumHint { get; init; }
        public bool IsBlurred { get; init; }
        public string? PreviewText { get; init; }
    }
}
