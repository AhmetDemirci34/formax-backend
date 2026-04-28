using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.SelfAudit
{
    public sealed class AiSelfAuditResult
    {
        public bool IsValid { get; init; }
        public bool ShouldRetract { get; init; }
        public string Reason { get; init; } = string.Empty;
        public double EffectiveConfidence { get; init; }
    }
}

