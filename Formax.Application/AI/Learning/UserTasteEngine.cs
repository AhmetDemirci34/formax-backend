using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Learning
{
    public class UserTasteEngine
    {
        public UserTasteProfile Update(
            UserTasteProfile profile,
            string confidenceLabel,
            bool isTrending,
            bool oddsDrop,
            bool liked)
        {
            var lr = 0.05;

            profile.TotalSwipes++;

            // RISK
            if (liked && confidenceLabel == "RISKY")
                profile.RiskLevel += lr;

            if (liked && confidenceLabel == "SAFE")
                profile.RiskLevel -= lr;

            // TREND
            if (isTrending)
                profile.TrendAffinity += liked ? lr : -lr;

            // VALUE
            if (oddsDrop)
                profile.ValueSeeking += liked ? lr : -lr;

            profile.RiskLevel = Clamp(profile.RiskLevel);
            profile.TrendAffinity = Clamp(profile.TrendAffinity);
            profile.ValueSeeking = Clamp(profile.ValueSeeking);

            return profile;
        }

        private double Clamp(double v) => Math.Max(0, Math.Min(1, v));
    }
}
