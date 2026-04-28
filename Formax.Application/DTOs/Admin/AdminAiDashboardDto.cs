using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Admin
{
    public class AdminAiDashboardDto
    {
        public AdminAiSnapshotDto Snapshot { get; set; } = new();
        public AdminAiTimelineDto Timeline { get; set; } = new();
    }
}
