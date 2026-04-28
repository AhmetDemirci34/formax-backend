using Formax.Application.DTOs.Home;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Radar
{
    public class RadarFilterService
    {
        public List<HomeRadarMatchDto> Filter(List<HomeRadarMatchDto> matches)
        {
            return matches
                // 🔥 sadece tamamen boşları at
                .Where(m => m.RadarScore > 10)
                .ToList();
        }
    }
}