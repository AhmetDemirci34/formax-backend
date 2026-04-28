using System.Collections.Generic;

namespace Formax.Application.DTOs.Home
{
    public sealed class HomeLiveSignalResponseDto
    {
        public string? SummaryText { get; init; }
        public IReadOnlyList<HomeLiveSignalMatchDto> Matches { get; init; } = new List<HomeLiveSignalMatchDto>();
    }
}
