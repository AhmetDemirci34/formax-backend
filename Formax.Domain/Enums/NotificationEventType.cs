namespace Formax.Domain.Enums
{
    /// <summary>
    /// Bildirimin türü. Frontend bu değere göre ikon seçer.
    /// Unknown = 0 → eski (legacy) kayıtlar için güvenli varsayılan.
    /// </summary>
    public enum NotificationEventType
    {
        Unknown = 0,
        Goal,
        Lineup,
        AIAnalysis,
        MatchStarted,
        MatchFinished,
        Transfer,
        News,
        RedCard,
        YellowCard,
        KickOff,
        AICombo
    }
}
