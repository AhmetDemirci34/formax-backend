using System.Collections.Generic;

namespace Formax.Application.DTOs.Admin
{
    /// <summary>
    /// Admin dashboard için birleşik görünüm DTO’su.
    /// (Timeline + Insights)
    /// </summary>
    public class AdminDashboardViewDto
    {
        public AdminAiTimelineDto Timeline { get; set; } = default!;
        public IReadOnlyList<string> Insights { get; set; } = new List<string>();

        // 🔥 SPRINT-14 — RISK FLAGS
        public List<AdminAiRiskFlagDto> Risks { get; set; } = new();

    }
}
