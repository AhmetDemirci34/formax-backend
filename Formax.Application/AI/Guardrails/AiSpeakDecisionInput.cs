using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Formax.Application.AI.Guardrails
{
    public sealed class AiSpeakDecisionInput
    {
        public bool ContextInsufficient { get; init; }
        public bool FatigueLimitReached { get; init; }
        public bool RepetitionBlocked { get; init; }
        public bool StateAllowsSpeaking { get; init; }
        public DateTime? LastSpeakAtUtc { get; init; }
        public System.TimeSpan MinimumSilenceDuration { get; set; }
    }
}
