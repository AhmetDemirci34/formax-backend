using System;
using Formax.Application.Services.Matches;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KADRO YOKLAMA TAKVİMİ — T−90 penceresi, altı slot, restart-safe son kontrol.
///
/// ÖLÇÜLEN HATA (06.09.2026): pencere T−45'te açılıyordu, arayüz ise "maçtan 1 saat
/// önce açıklanacak" diye KESİN söz veriyordu. 11.09.2026'da takvim T−90/60/30/15/10/5
/// slotlarına genişletildi ve karar deneme SAYISINDAN son GERÇEK kontrol anına taşındı
/// (bkz. <see cref="LineupDeliveryTests"/>).
///
/// Bu testler saf karar fonksiyonunu sınar: ağ yok, veritabanı yok.
/// </summary>
public class LineupPollScheduleTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 6, 17, 0, 0, DateTimeKind.Utc);

    private static DateTime Minus(int minutes) => Kickoff.AddMinutes(-minutes);

    [Fact]
    public void T90_AdayOlur()
    {
        Assert.Equal(0, LineupPollSchedule.DueSlot(Kickoff, Minus(90)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(90), false, null));
    }

    [Theory]
    [InlineData(91)]
    [InlineData(120)]
    [InlineData(360)]
    [InlineData(60 * 24)]
    public void T90danErken_AdayOlmaz(int minutesBefore)
    {
        Assert.Null(LineupPollSchedule.DueSlot(Kickoff, Minus(minutesBefore)));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(minutesBefore), false, null));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(60)]
    [InlineData(30)]
    [InlineData(15)]
    [InlineData(10)]
    [InlineData(5)]
    public void BasariliLineup_YenidenIstenmez(int minutesBefore)
        => Assert.False(LineupPollSchedule.ShouldPoll(
            Kickoff, Minus(minutesBefore), lineupAlreadyComplete: true, lastRealCheckUtc: null));

    [Fact]
    public void KickofftanOnDakikaSonra_YoklamaBiter()
    {
        Assert.Equal(5, LineupPollSchedule.DueSlot(Kickoff, Kickoff));
        Assert.Equal(5, LineupPollSchedule.DueSlot(Kickoff, Kickoff.AddMinutes(10)));
        Assert.Null(LineupPollSchedule.DueSlot(Kickoff, Kickoff.AddMinutes(11)));
        Assert.Null(LineupPollSchedule.DueSlot(Kickoff, Kickoff.AddMinutes(30)));
    }

    /// <summary>HER SLOTTAN EN FAZLA BİR GERÇEK KONTROL; altı slot bitince istek yok.</summary>
    [Fact]
    public void HerSlottanEnFazlaBirKontrol()
    {
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(90), false, null));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(80), false, Minus(90)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(60), false, Minus(90)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(30), false, Minus(60)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(15), false, Minus(30)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(10), false, Minus(15)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(5), false, Minus(10)));

        // T−5 kontrolünden sonra yakalama payında bile yeni istek YOK.
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(2), false, Minus(5)));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Kickoff.AddMinutes(8), false, Minus(5)));
    }

    /// <summary>
    /// RESTART HARCANMIŞ SLOTU GERİ GETİRMEZ — son kontrol anı KALICI kayıttan gelir.
    /// </summary>
    [Fact]
    public void RestartSonrasi_HarcanmisSlotGeriGelmez()
        => Assert.False(LineupPollSchedule.ShouldPoll(
            Kickoff, Minus(20), lineupAlreadyComplete: false, lastRealCheckUtc: Minus(28)));

    [Fact]
    public void AtlananSlot_HakkiYakmaz()
        // İş T−90 ve T−60 turlarında hiç çalışmadıysa (ör. bakım), T−30'da hâlâ hak var.
        => Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(30), false, null));
}
