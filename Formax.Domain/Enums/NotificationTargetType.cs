namespace Formax.Domain.Enums
{
    /// <summary>
    /// Bildirime tıklanınca frontend'in yönleneceği hedef tipi.
    /// TargetId ile birlikte kullanılır (ör. Match → matchId).
    /// Match = 0 → legacy kayıtlar için güvenli varsayılan (Match Detail).
    /// </summary>
    public enum NotificationTargetType
    {
        Match = 0,
        MatchLineup,
        MatchAIAnalysis,
        News,
        AICombo,
        None
    }
}
