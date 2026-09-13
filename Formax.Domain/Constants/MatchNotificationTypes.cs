namespace Formax.Domain.Constants
{
    /// <summary>
    /// MAÇ BİLDİRİM TÜRLERİ — <c>UserNotification.NotificationType</c> değerleri ve
    /// DB seviyesindeki tekillik anahtarlarının biçimi. Anahtar kalıbı tek yerde durur ki
    /// iki job aynı olay için iki farklı anahtar üretemesin.
    /// </summary>
    public static class MatchNotificationTypes
    {
        public const string LineupAvailable = "MATCH_LINEUP_AVAILABLE";
        public const string CriticalUpdate = "MATCH_CRITICAL_UPDATE";

        /// <summary>Bildirime basınca açılacak maç detayı rotası.</summary>
        public static string MatchRoute(int matchId) => $"/match/{matchId}";

        public static string LineupKey(int matchId, int userId)
            => $"{LineupAvailable}:{matchId}:{userId}";

        public static string CriticalKey(int matchId, string evidenceHash, int userId)
            => $"{CriticalUpdate}:{matchId}:{evidenceHash}:{userId}";

        /// <summary>Bildirim Tercihleri ekranındaki maç anahtarı.</summary>
        public static string MatchPreferenceKey(int matchId) => $"match:{matchId}";
    }
}
