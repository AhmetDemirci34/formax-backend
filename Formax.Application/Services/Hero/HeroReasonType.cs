namespace Formax.Application.Services.Hero
{
    /// <summary>
    /// Hero oyuncusunun neden seçildiğinin tipi (string yerine enum).
    /// HeroSelectionEngine, baskın Hero'nun PrimaryReason'ını bu tipe eşler.
    /// Narrative bu tipten daha sonra HeroAggregate katmanında üretilir.
    /// </summary>
    public enum HeroReasonType
    {
        HighestForm = 0,
        MostTalked = 1,
        TopScorer = 2,
        Playmaker = 3,
        MatchChanger = 4,
        FanFavourite = 5,
        Captain = 6,
    }
}
