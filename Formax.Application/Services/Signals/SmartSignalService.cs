using Formax.Application.DTOs.Home;
using System.Collections.Generic;

namespace Formax.Application.Services.Signals
{
    public class SmartSignalService
    {
        public List<string> Generate(HomeRadarMatchDto match)
        {
            var signals = new List<string>();

            // 🔥 GOAL HEAVY
            if (match.MatchHeatScore > 70)
                signals.Add("🔥 High goal potential");

            // 🔥 HOT MATCH
            if (match.RadarScore > 80)
                signals.Add("⚡ High confidence");

            // 🔥 USER INTEREST
            if (match.TeamInterestScore > 60)
                signals.Add("👤 Matches your interest");

            // 🔥 TREND
            if (match.BehaviorMomentumScore > 50)
                signals.Add("📈 Trending match");

            // fallback
            if (signals.Count == 0)
                signals.Add("📊 Balanced opportunity");

            return signals;
        }
    }
}
