using Formax.Application.DTOs.Admin;
using Formax.Application.Interfaces;
using System;
using System.Linq;

namespace Formax.Application.Services.AdminDashboard
{
    public class AiSpeakMetricsCalculator
    {
        private readonly IAiSpeakTelemetryReadRepository _readRepository;

        public AiSpeakMetricsCalculator(
            IAiSpeakTelemetryReadRepository readRepository)
        {
            _readRepository = readRepository;
        }

        public AiSpeakMetricsDto Calculate()
        {
            var all = _readRepository.GetAll();

            var total = all.Count;

            var speakCount = all.Count(x => x.CanSpeak);
            var silenceCount = total - speakCount;

            var contextInsufficient =
                all.Count(x => x.SilenceReason == "ContextInsufficient");

            var rateLimited =
                all.Count(x => x.SilenceReason == "RateLimited");

            var stateBlocked =
                all.Count(x => x.SilenceReason == "StateNotAllowed");

            return new AiSpeakMetricsDto
            {
                TotalDecisions = total,

                SpeakCount = speakCount,
                SilenceCount = silenceCount,

                ContextInsufficientCount = contextInsufficient,
                RateLimitedCount = rateLimited,
                StateBlockedCount = stateBlocked,

                SpeakRate = total == 0 ? 0 : Math.Round((double)speakCount / total, 3),
                SilenceRate = total == 0 ? 0 : Math.Round((double)silenceCount / total, 3)
            };
        }
    }
}
