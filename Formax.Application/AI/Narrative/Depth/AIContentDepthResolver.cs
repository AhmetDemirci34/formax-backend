namespace Formax.Application.AI.Narrative.Depth
{
    public sealed class AIContentDepthResolver
    {
        public AIContentDepth Resolve(bool isPremium)
        {
            if (isPremium)
            {
                return AIContentDepth.Standard;
            }

            return AIContentDepth.Summary;
        }
    }
}
