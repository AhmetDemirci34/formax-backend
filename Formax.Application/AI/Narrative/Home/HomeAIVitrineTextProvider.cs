using Formax.Application.AI.Narrative.Depth;

namespace Formax.Application.AI.Narrative.Home
{
    public sealed class HomeAIVitrineTextProvider : IHomeNarrativeBuilder
    {
        public string Build(AIContentDepth depth)
        {
            return depth switch
            {
                AIContentDepth.Summary =>
                    "Bugünkü maçlara genel bir çerçeveden bakıyorum.",

                AIContentDepth.Standard =>
                    "Bugünkü maçlarda öne çıkan dengeleri ve bağlamları değerlendiriyorum.",

                AIContentDepth.Deep =>
                    "Bugünkü maçlarda takımların form durumları, psikolojik eşikler ve maç içi olası senaryoları detaylıca ele alıyorum.",

                _ => string.Empty
            };
        }
    }
}
