using System;
using Formax.Infrastructure.Repositories;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// DÖRT TAKVİM GÜNÜ PENCERESİ (Europe/Istanbul).
///
/// Eski pencere <c>utcNow.AddDays(3)</c> = KAYAN 72 SAATTİ ve dördüncü günün akşam
/// maçlarını saat farkı kadar dışarıda bırakıyordu. Ürün davranışı "bugün dâhil dört
/// TAKVİM günü"dür.
/// </summary>
public class FourCalendarDayWindowTests
{
    private static readonly TimeZoneInfo Tr =
        Formax.Infrastructure.Http.ApiFootballTimeZone.TryResolve("Europe/Istanbul")!;

    [Fact]
    public void DorduncuGununAksamMaci_PencereyeGirer()
    {
        // 1 Eylül 20:21 UTC (TR 23:21). Dördüncü gün = 4 Eylül.
        var now = new DateTime(2026, 9, 1, 20, 21, 25, DateTimeKind.Utc);
        var end = MatchReadRepository.FourCalendarDayWindowEndUtc(now);

        // Başakşehir–Galatasaray: TR 4 Eylül 20:00 = 17:00 UTC
        Assert.True(new DateTime(2026, 9, 4, 17, 0, 0) <= end);

        // ESKİ KAYAN PENCERE bu maçı kapsardı ama 4 Eylül'ün GEÇ maçlarını kaçırırdı:
        var oldRolling = now.AddDays(3);                       // 04.09 20:21 UTC
        var lateMatch  = new DateTime(2026, 9, 4, 21, 0, 0);    // TR 05.09 00:00
        Assert.False(lateMatch <= oldRolling);                 // eski pencerede DÜŞERDİ
        Assert.True(lateMatch <= end);                         // yeni pencerede GİRER
    }

    [Fact]
    public void PencereSonu_DorduncuTurkiyeGununSonudur()
    {
        var now = new DateTime(2026, 9, 1, 20, 21, 25, DateTimeKind.Utc);
        var end = MatchReadRepository.FourCalendarDayWindowEndUtc(now);

        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(end, DateTimeKind.Utc), Tr);

        // 5 Eylül 00:00 TR — yani 4 Eylül'ün tamamı dâhil, 5 Eylül hariç.
        Assert.Equal(new DateTime(2026, 9, 5, 0, 0, 0), localEnd);
    }

    [Fact]
    public void GunBasindaVeGunSonunda_AyniTakvimPenceresi()
    {
        // Aynı Türkiye gününün sabahı ve gecesi AYNI pencere sonunu vermeli;
        // kayan pencerede bu iki değer 20 saat farklı olurdu.
        var morning = new DateTime(2026, 9, 1, 5, 0, 0, DateTimeKind.Utc);   // TR 08:00
        var night   = new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc);  // TR 23:00

        Assert.Equal(
            MatchReadRepository.FourCalendarDayWindowEndUtc(morning),
            MatchReadRepository.FourCalendarDayWindowEndUtc(night));
    }

    [Fact]
    public void TurkiyeSaatinden_UTCye_DonusumDogru()
    {
        // TR 4 Eylül 20:00 → 17:00 UTC (UTC+3)
        var local = new DateTime(2026, 9, 4, 20, 0, 0, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Tr);

        Assert.Equal(new DateTime(2026, 9, 4, 17, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void GeceYarisiniAsanTRMaci_DorduncuGunde_KapsanIr()
    {
        var now = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc);   // TR 06:00
        var end = MatchReadRepository.FourCalendarDayWindowEndUtc(now);

        // TR 4 Eylül 23:45 = 20:45 UTC — dördüncü günün en geç maçı.
        Assert.True(new DateTime(2026, 9, 4, 20, 45, 0) <= end);
        // TR 5 Eylül 00:15 = 21:15 UTC — beşinci gün, KAPSAM DIŞI.
        Assert.False(new DateTime(2026, 9, 4, 21, 15, 0) <= end);
    }
}
