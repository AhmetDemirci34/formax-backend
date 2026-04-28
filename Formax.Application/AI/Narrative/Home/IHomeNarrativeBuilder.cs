using Formax.Application.AI.Narrative.Depth;

namespace Formax.Application.AI.Narrative.Home
{
    public interface IHomeNarrativeBuilder
    {
        string Build(AIContentDepth depth);
    }
}
