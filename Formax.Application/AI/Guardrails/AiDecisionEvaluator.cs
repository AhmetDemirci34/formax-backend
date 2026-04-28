using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public class AiDecisionEvaluator
    {
        public AiDecisionResult Evaluate(double confidenceScore)
        {
            if (confidenceScore < AiDecisionLimits.SilenceThreshold)
            {
                return AiDecisionResult.Silent();
            }

            if (confidenceScore < AiDecisionLimits.MinConfidenceToExpand)
            {
                return AiDecisionResult.Basic();
            }

            if (confidenceScore > AiDecisionLimits.MaxConfidenceCeiling)
            {
                confidenceScore = AiDecisionLimits.MaxConfidenceCeiling;
            }

            return AiDecisionResult.Extended(confidenceScore);
        }
    }
}

