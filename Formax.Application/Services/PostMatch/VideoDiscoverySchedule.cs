using System;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// KALICI VİDEO KEŞFİ — tekrar planı ve ekran durumu. Saf fonksiyonlar; iş ve detay ucu aynı kuralı okur.
    ///
    /// Plan (maç bitişinden itibaren): 15 dk, 30 dk, 60 dk, 2 sa, 4 sa, 8 sa, 12 sa, 24 sa; sonra 7 gün
    /// boyunca günde bir; sonra 30. güne kadar haftada iki kez (3,5 günde bir); sonra haftada bir — video bulunana dek. Geçmiş maçlarda (backfill) plan saatleri çoktan geçmiştir;
    /// bu yüzden bir deneme, önceki GERÇEK denemeden en az ilgili aralık kadar sonra yapılır — geçmiş maç
    /// tek turda 15 kez art arda taranmaz.
    /// </summary>
    public static class VideoDiscoverySchedule
    {
        public static readonly TimeSpan[] FirstDayOffsets =
        {
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60), TimeSpan.FromHours(2),
            TimeSpan.FromHours(4), TimeSpan.FromHours(8), TimeSpan.FromHours(12), TimeSpan.FromHours(24)
        };

        public const int DailyAttempts = 7;

        /// <summary>İlk 24 saat + 7 gün sonrası, 30. güne kadar haftada iki kez.</summary>
        public static readonly TimeSpan TwiceWeeklyGap = TimeSpan.FromHours(84);

        /// <summary>30. günden sonra.</summary>
        public static readonly TimeSpan WeeklyGap = TimeSpan.FromDays(7);

        /// <summary>Haftada iki denemenin süreceği son gün (maç bitişine göre).</summary>
        public static readonly TimeSpan TwiceWeeklyUntil = TimeSpan.FromDays(30);

        /// <summary>İlk günün aktif arama turu sayısı; bitene kadar ekran "aranıyor" der.</summary>
        public static int SearchingAttempts => FirstDayOffsets.Length;

        /// <summary>Teknik hata/engel sonrası tekrar (deneme sayılmaz).</summary>
        public static readonly TimeSpan FailedRetry = TimeSpan.FromMinutes(30);

        /// <summary>k. denemenin (0 tabanlı) maç bitişine göre plan anı.</summary>
        public static DateTime PlannedAt(DateTime endUtc, int attemptIndex)
        {
            if (attemptIndex < 0) attemptIndex = 0;
            if (attemptIndex < FirstDayOffsets.Length) return endUtc + FirstDayOffsets[attemptIndex];
            var daily = attemptIndex - FirstDayOffsets.Length + 1;
            if (daily <= DailyAttempts) return endUtc + TimeSpan.FromHours(24) + TimeSpan.FromDays(daily);
            var afterDaily = endUtc + TimeSpan.FromHours(24) + TimeSpan.FromDays(DailyAttempts);
            var twiceWeeklyCount = (int)Math.Floor((TwiceWeeklyUntil - (afterDaily - endUtc)).Ticks / (double)TwiceWeeklyGap.Ticks);
            var k = daily - DailyAttempts;
            if (k <= twiceWeeklyCount) return afterDaily + TimeSpan.FromTicks(TwiceWeeklyGap.Ticks * k);
            return afterDaily + TimeSpan.FromTicks(TwiceWeeklyGap.Ticks * twiceWeeklyCount) + TimeSpan.FromTicks(WeeklyGap.Ticks * (k - twiceWeeklyCount));
        }

        /// <summary>k. deneme ile bir önceki arasındaki plan aralığı (en az 15 dk).</summary>
        public static TimeSpan Gap(int attemptIndex)
        {
            if (attemptIndex <= 0) return TimeSpan.Zero;
            var gap = PlannedAt(DateTime.MinValue.AddYears(1), attemptIndex) - PlannedAt(DateTime.MinValue.AddYears(1), attemptIndex - 1);
            return gap < TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : gap;
        }

        /// <summary>Sıradaki denemenin anı: plan anı ile "son denemeden en az aralık kadar sonra"nın büyüğü.</summary>
        public static DateTime NextAttempt(DateTime endUtc, int completedAttempts, DateTime? lastAttemptUtc)
        {
            var planned = PlannedAt(endUtc, completedAttempts);
            if (lastAttemptUtc is DateTime last)
            {
                var spaced = last + Gap(completedAttempts);
                if (spaced > planned) return spaced;
            }
            return planned;
        }
    }

    /// <summary>Kuyruk/ekran durumları — kalıcı defterden okunur, saatten türetilmez.</summary>
    public static class VideoDiscoveryStates
    {
        public const string Searching = "Searching";
        public const string FullHighlightsAvailable = "FullHighlightsAvailable";
        public const string GoalClipsAvailable = "GoalClipsAvailable";
        public const string NotAvailableYet = "NotAvailableYet";
        public const string SourceBlocked = "SourceBlocked";
        public const string Failed = "Failed";

        /// <summary>
        /// Bir keşif turundan sonraki durum. Oynatılabilir tam özet her şeyin önündedir; yalnız gol klibi
        /// varsa GoalClipsAvailable; teknik hata Failed; doğru ama gömülemeyen video SourceBlocked; aksi hâlde
        /// ilk günün turları bitene kadar Searching, sonra NotAvailableYet (terminal DEĞİL, arama sürer).
        /// </summary>
        public static string Resolve(bool playableFull, bool playableGoals, bool verifiedButBlocked,
            bool technicalFailure, int completedAttempts)
        {
            if (playableFull) return FullHighlightsAvailable;
            if (playableGoals) return GoalClipsAvailable;
            if (technicalFailure) return Failed;
            if (verifiedButBlocked) return SourceBlocked;
            return completedAttempts < VideoDiscoverySchedule.SearchingAttempts ? Searching : NotAvailableYet;
        }

        /// <summary>Tam özet bulununca tekrar durur; diğer bütün durumlar planlı olarak yeniden aranır.</summary>
        public static bool StopsRetrying(string state) => state == FullHighlightsAvailable;
    }
}
