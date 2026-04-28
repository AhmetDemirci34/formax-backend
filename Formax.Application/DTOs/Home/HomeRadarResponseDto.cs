using System.Collections.Generic;

namespace Formax.Application.DTOs.Home
{
    public sealed class HomeRadarResponseDto
    {
        public string? BannerText { get; init; }
        public HomeContentAccessDto ContentAccess { get; init; } = new();
        public IReadOnlyList<HomeRadarMatchDto> Matches { get; init; } = new List<HomeRadarMatchDto>();
    }
}
