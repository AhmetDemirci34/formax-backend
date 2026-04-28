using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.AI
{
    public class AINarrativeDto
    {
        public bool IsSilent { get; init; }

        public string? Title { get; init; }

        public string? Body { get; init; }

        public string? SilentReason { get; init; }
    }
}
