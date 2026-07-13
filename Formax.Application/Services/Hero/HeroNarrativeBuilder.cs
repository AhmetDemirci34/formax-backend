using Formax.Application.Interfaces;

namespace Formax.Application.Services.Hero
{
    /// <summary>
    /// FORMAX HeroNarrativeBuilder — HeroReasonType'a göre kısa Hero anlatısı üretir.
    /// Template tabanlı, deterministik, LLM YOK.
    /// </summary>
    public sealed class HeroNarrativeBuilder : IHeroNarrativeBuilder
    {
        public string Build(HeroSelectionResult selection) => selection.HeroReasonType switch
        {
            HeroReasonType.HighestForm => "En formda oyuncu.",
            HeroReasonType.MostTalked => "Son haftaların en çok konuşulan ismi.",
            HeroReasonType.TopScorer => "Bu maçın en golcü ismi.",
            HeroReasonType.Playmaker => "Oyunu kuran isim.",
            HeroReasonType.MatchChanger => "Maçı değiştirebilecek oyuncu.",
            HeroReasonType.FanFavourite => "Taraftarın gözdesi.",
            HeroReasonType.Captain => "Takımın lideri.",
            _ => "Bu maçın kilit oyuncusu.",
        };
    }
}
