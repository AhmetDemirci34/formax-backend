using Formax.Application.Services.Hero;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// HeroSelectionResult'tan HeroReasonType'a göre Hero Narrative üretir.
    /// Template tabanlı, LLM YOK. HeroAggregate bunu çağırır; kendisi metin üretmez.
    /// </summary>
    public interface IHeroNarrativeBuilder
    {
        string Build(HeroSelectionResult selection);
    }
}
