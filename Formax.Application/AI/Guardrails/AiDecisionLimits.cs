using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public static class AiDecisionLimits
    {
        // 🔒 Mutlak üst sınır
        public const double MaxConfidenceCeiling = 0.72;

        // 🔒 Bu seviyenin altı: konuşma ama genişleme
        public const double MinConfidenceToExpand = 0.60;

        // 🔒 Bunun altı: tamamen sus
        public const double SilenceThreshold = 0.45;
    }
}

