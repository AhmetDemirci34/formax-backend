using Formax.Domain.States;

namespace Formax.Application.States
{
    public class AIStateResult
    {
        public AIUxState State { get; set; }
        public bool ShouldSpeak { get; set; }
        public bool CanExpand { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public int MatchId { get; set; }

    }
}
