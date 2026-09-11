using System;
using Formax.Application.Services.Matches;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KADRO YOKLAMA TAKVİMİ — T−90 penceresi, dört slot, restart-safe hak.
///
/// ÖLÇÜLEN HATA (06.09.2026): pencere T−45'te açılıyordu, arayüz ise "maçtan 1 saat
/// önce açıklanacak" diye KESİN söz veriyordu. Maça 45–60 dakika kalan aralıkta sistem
/// henüz hiç sormamış oluyor, kullanıcı ise verilen sözle boş ekrana bakıyordu.
///
/// Bu testler saf karar fonksiyonunu sınar: ağ yok, veritabanı yok.
/// </summary>
public class LineupPollScheduleTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 6, 17, 0, 0, DateTimeKind.Utc);

    private static DateTime Minus(int minutes) => Kickoff.AddMinutes(-minutes);

    // ── 43. T−90 ADAY OLUR ──────────────────────────────────────────────────────
    [Fact]
    public void T90_AdayOlur()
    {
        Assert.Equal(0, LineupPollSchedule.SlotFor(Kickoff, Minus(90)));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(90), false, 0));
    }

    // ── 44. T−90'DAN ERKEN ADAY OLMAZ ───────────────────────────────────────────
    [Theory]
    [InlineData(91)]
    [InlineData(120)]
    [InlineData(360)]
    [InlineData(60 * 24)]
    public void T90danErken_AdayOlmaz(int minutesBefore)
    {
        Assert.Null(LineupPollSchedule.SlotFor(Kickoff, Minus(minutesBefore)));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(minutesBefore), false, 0));
    }

    // ── 45. BAŞARILI LINEUP YENİDEN İSTENMEZ ────────────────────────────────────
    [Theory]
    [InlineData(90)]
    [InlineData(60)]
    [InlineData(30)]
    [InlineData(10)]
    public void BasariliLineup_YenidenIstenmez(int minutesBefore)
    {
        // Kadro DB'de TAM: hangi slotta olursak olalım sağlayıcıya gidilmez.
        Assert.False(LineupPollSchedule.ShouldPoll(
            Kickoff, Minus(minutesBefore), lineupAlreadyComplete: true, attemptsSoFar: 0));
    }

    // ── 46. T−60 / T−30 / T−10 SLOTLARI ─────────────────────────────────────────
    [Theory]
    [InlineData(90, 0)]
    [InlineData(75, 0)]
    [InlineData(61, 0)]
    [InlineData(60, 1)]
    [InlineData(45, 1)]
    [InlineData(31, 1)]
    [InlineData(30, 2)]
    [InlineData(15, 2)]
    [InlineData(11, 2)]
    [InlineData(10, 3)]
    [InlineData(5, 3)]
    public void SlotSinirlari_Dogru(int minutesBefore, int expectedSlot)
        => Assert.Equal(expectedSlot, LineupPollSchedule.SlotFor(Kickoff, Minus(minutesBefore)));

    [Fact]
    public void PencereKapanisi_KickoffaBesDakikadanAz()
    {
        Assert.Null(LineupPollSchedule.SlotFor(Kickoff, Minus(4)));
        Assert.Null(LineupPollSchedule.SlotFor(Kickoff, Kickoff));            // kickoff anı
        Assert.Null(LineupPollSchedule.SlotFor(Kickoff, Kickoff.AddMinutes(30))); // maç başladı
    }

    /// <summary>
    /// HER SLOTTAN EN FAZLA BİR GERÇEK İSTEK. İş 5 dakikada bir dönüyor; sınır yalnız
    /// soğumaya bırakılsaydı kadrosu yayımlanmayan bir maç pencerede ~18 kez yoklanırdı.
    /// </summary>
    [Fact]
    public void HerSlottanEnFazlaBirIstek()
    {
        // Slot 0 (T−90…T−61) içinde ilk istek yapıldıktan sonra aynı slotta bir daha yok.
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(90), false, 0));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(80), false, 1));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(70), false, 1));

        // Slot 1'e geçince (T−60) hak yeniden açılır.
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(60), false, 1));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(50), false, 2));

        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(30), false, 2));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(10), false, 3));

        // Dört slot bitti: pencerede kalsa bile beşinci istek YOK.
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, Minus(8), false, 4));
    }

    /// <summary>
    /// RESTART GÜNLÜK HAKKI SIFIRLAMAZ — deneme sayısı KALICI defterden gelir.
    ///
    /// Bu test, sayının nereden okunduğunu sözleşme olarak sabitler: süreç yeniden
    /// başladığında sayaç 0'a dönseydi aşağıdaki çağrı true dönerdi ve aynı maç
    /// her açılışta yeniden yoklanırdı (kadro, kotanın en pahalı kalemidir).
    /// </summary>
    [Fact]
    public void RestartSonrasi_HarcanmisSlotGeriGelmez()
    {
        const int attemptsFromPersistentLedger = 4;

        Assert.False(LineupPollSchedule.ShouldPoll(
            Kickoff, Minus(20), lineupAlreadyComplete: false,
            attemptsSoFar: attemptsFromPersistentLedger));
    }

    [Fact]
    public void AtlananSlot_HakkiYakmaz()
    {
        // İş T−90 ve T−60 turlarında hiç çalışmadıysa (ör. bakım), T−30'da hâlâ hak var.
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, Minus(30), false, 0));
    }

    [Fact]
    public void PencereSabitleri_UrunKararinaUyar()
    {
        Assert.Equal(TimeSpan.FromMinutes(90), LineupPollSchedule.WindowOpen);
        Assert.Equal(TimeSpan.FromMinutes(5), LineupPollSchedule.WindowClose);
        Assert.Equal(4, LineupPollSchedule.SlotCount);
        Assert.Equal(new[] { 90, 60, 30, 10 }, LineupPollSchedule.SlotBoundariesMinutes);
    }
}
