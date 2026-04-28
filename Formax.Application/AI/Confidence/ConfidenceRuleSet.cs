using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Confidence
{
    public static class ConfidenceRuleSet
    {
        // PreMatch
        public const double LineupAnnouncedBonus = 0.10;
        public const double WorldHighBonus = 0.10;

        // Live
        public const double LiveMinute15Bonus = 0.15;
        public const double MajorEventBonus = 0.15;

        // Decay
        public const double RecentExtendedPenalty = -0.20;

        // Threshold
        public const double LlmSpeakThreshold = 0.85;
    }
}

