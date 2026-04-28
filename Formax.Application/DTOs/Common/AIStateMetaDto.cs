using Formax.Domain.States;

namespace Formax.Application.DTOs.Common
{
    public class AIStateMetaDto
    {
        public AIUxState State { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public bool CanExpand { get; set; }
    }
}