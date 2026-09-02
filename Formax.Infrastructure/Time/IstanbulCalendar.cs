using System;
using Formax.Infrastructure.Http;

namespace Formax.Infrastructure.Time
{
    /// <summary>
    /// TÜRKİYE TAKVİM GÜNÜ ↔ UTC ARALIĞI — sonuç listesinin gün sınırının TEK kaynağı.
    ///
    /// NEDEN AYRI BİR YER: "26 Ağustos maçları" cümlesi UTC'de bir gün DEĞİLDİR. Türkiye
    /// UTC+3 olduğu için 26 Ağustos Türkiye günü, UTC'de 25.08 21:00 ile 26.08 21:00
    /// arasıdır. Bu hesabı her çağıranın kendi yapması, 21:00–00:00 arası başlayan
    /// maçların bir gün ileri/geri kaymasına yol açar — Şampiyonlar Ligi maçlarının
    /// çoğu tam o saatte oynanır.
    ///
    /// Saat dilimi çözülemezse UTC'ye düşülür (davranış bozulmaz, yalnız sınır UTC olur);
    /// sahte bir ofset UYDURULMAZ.
    /// </summary>
    public static class IstanbulCalendar
    {
        public static TimeZoneInfo? Zone => ApiFootballTimeZone.TryResolve(ApiFootballTimeZone.DefaultId);

        /// <summary>Verilen UTC anının denk geldiği Türkiye takvim günü.</summary>
        public static DateOnly TodayIn(DateTime utcNow)
        {
            var tz = Zone;
            if (tz == null) return DateOnly.FromDateTime(utcNow.Date);
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
            return DateOnly.FromDateTime(local.Date);
        }

        /// <summary>
        /// Bir Türkiye takvim gününün UTC aralığı: [başlangıç, bitiş). Bitiş DAHİL DEĞİLDİR
        /// — 21:00:00.000 tam sınırdır ve o an ertesi güne aittir.
        /// </summary>
        public static (DateTime StartUtc, DateTime EndUtcExclusive) DayRangeUtc(DateOnly istanbulDay)
        {
            var tz = Zone;
            var localStart = istanbulDay.ToDateTime(TimeOnly.MinValue);
            var localEnd = istanbulDay.AddDays(1).ToDateTime(TimeOnly.MinValue);

            if (tz == null)
                return (DateTime.SpecifyKind(localStart, DateTimeKind.Utc),
                        DateTime.SpecifyKind(localEnd, DateTimeKind.Utc));

            return (
                TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz),
                TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), tz));
        }
    }
}
