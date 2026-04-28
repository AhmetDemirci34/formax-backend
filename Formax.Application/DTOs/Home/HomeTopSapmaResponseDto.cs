using System.Collections.Generic;

namespace Formax.Application.DTOs.Home
{
    public sealed class HomeTopSapmaResponseDto
    {
        public string? BannerText { get; init; }
        public IReadOnlyList<HomeTopSapmaMatchDto> Matches { get; init; } = new List<HomeTopSapmaMatchDto>();
    }
}
