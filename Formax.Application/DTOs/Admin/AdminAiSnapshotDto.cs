using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Admin
{
    public class AdminAiSnapshotDto
    {
        public int TotalDecisions { get; set; }

        public int Extended { get; set; }
        public int Short { get; set; }
        public int Silent { get; set; }
        public int SelfRetracted { get; set; }

        public string[] Insights { get; set; } = [];
    }
}
