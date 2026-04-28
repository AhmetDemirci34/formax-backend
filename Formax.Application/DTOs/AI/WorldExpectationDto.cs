using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.AI
{
    public sealed class WorldExpectationDto
    {
        public int ConfidenceLevel { get; set; }   // 0–100
        public string Summary { get; set; } = string.Empty;
        public bool IsStale { get; set; }
        public DateTime? GeneratedAt { get; set; }
    }
}
