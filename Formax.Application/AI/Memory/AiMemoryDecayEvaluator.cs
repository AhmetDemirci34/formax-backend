using System;

namespace Formax.Application.AI.Memory
{
    public class AiMemoryDecayEvaluator
    {
        public bool IsExpired(DateTime? lastInteractionAt)
        {
            if (!lastInteractionAt.HasValue)
                return true;

            var daysPassed =
                (DateTime.UtcNow - lastInteractionAt.Value).TotalDays;

            return daysPassed > AiMemoryDecayPolicy.ExtendedContextRetentionDays;
        }
    }
}
