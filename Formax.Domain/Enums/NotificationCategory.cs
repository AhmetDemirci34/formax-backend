namespace Formax.Domain.Enums
{
    /// <summary>
    /// Bildirim kategorisi. Frontend segment filtrelemede kullanır.
    /// Match = 0 → tüm mevcut bildirimler maç kaynaklı olduğundan legacy varsayılan.
    /// "All" yalnızca filtre amaçlı bir değerdir; bir bildirime atanmaz.
    /// </summary>
    public enum NotificationCategory
    {
        Match = 0,
        Team,
        League,
        News,
        All
    }
}
