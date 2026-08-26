namespace Formax.Application.DTOs.Diagnostics
{
    /// <summary>
    /// api-football GET /status yanıtı — GERÇEK plan limiti ve günlük kullanım.
    /// Quota Intelligence için (varsayım yerine ölçülmüş plan limiti).
    /// </summary>
    public sealed class SportsApiStatus
    {
        public int RequestsToday { get; init; }
        public int DailyLimit { get; init; }
        public string PlanName { get; init; } = "";
    }
}
