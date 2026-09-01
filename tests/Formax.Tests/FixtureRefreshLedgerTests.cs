using System;
using System.Linq;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KALICI DENEME DEFTERİ — restart tekil istek patlamasını TEKRARLAYAMAZ.
///
/// Ölçülen hata (01.09.2026): tavan yalnız tur başınaydı ve süreç belleğindeydi; her
/// açılış 30 sn sonra yeni tur başlatıyor ve aynı 10 isteklik patlama tekrarlanıyordu
/// (5 açılış = 44 tekil istek). Buradaki testler defterin restart'tan etkilenmediğini
/// kanıtlar: "restart" = aynı veritabanı üzerinde YENİ repository örneği.
/// </summary>
public class FixtureRefreshLedgerTests : IDisposable
{
    private readonly DbContextOptions<FormaxDbContext> _options;
    private readonly FormaxDbContext _db;

    private static readonly DateTime Now = new(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc);

    public FixtureRefreshLedgerTests()
    {
        _options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"ledger-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(_options);
    }

    /// <summary>Süreç yeniden başlamış gibi YENİ bağlam + repository verir (aynı DB).</summary>
    private FixtureSyncRepository RestartedProcess()
        => new(new FormaxDbContext(_options));

    private FixtureSyncRepository Repo() => new(_db);

    [Fact]
    public void Restart_SogumaIcindeAyniFikstruYenidenDENEMEZ()
    {
        var first = Repo().TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now);
        Assert.True(first);

        // SÜREÇ YENİDEN BAŞLADI — defter kalıcı olduğu için soğuma korunur.
        var afterRestart = RestartedProcess().TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now.AddMinutes(31));

        Assert.False(afterRestart);
    }

    [Fact]
    public void SogumaDolunca_YenidenDenenebilir()
    {
        Repo().TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now);

        var later = RestartedProcess().TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now.AddHours(13));

        Assert.True(later);
    }

    [Fact]
    public void BasarisizSaglayiciCevabi_DenemeyiYineDeKalicilastirir()
    {
        var repo = Repo();
        Assert.True(repo.TryReserveFixtureAttempt(
            "9001", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now));

        // Sağlayıcı hata döndü — rezervasyon GERİ ALINMAZ, yalnız sonuç işaretlenir.
        repo.RecordFixtureAttemptOutcome("9001", FixtureRefreshPurposes.Result, Now, "ProviderError");
        _db.SaveChanges();

        var retry = RestartedProcess().TryReserveFixtureAttempt(
            "9001", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now.AddMinutes(5));

        Assert.False(retry);   // bozuk fikstür kotayı dövemez
        var row = _db.FixtureRefreshAttempts.AsNoTracking().Single();
        Assert.Equal("ProviderError", row.LastOutcome);
        Assert.Equal(1, row.AttemptCount);
    }

    [Fact]
    public void GunlukTavan_RestartSonrasiKorunur()
    {
        var repo = Repo();
        for (var i = 0; i < 10; i++)
            Assert.True(repo.TryReserveFixtureAttempt(
                $"fx{i}", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now));

        // Tavan doldu; YENİ SÜREÇ bile 11'inciyi alamaz.
        var afterRestart = RestartedProcess().TryReserveFixtureAttempt(
            "fx99", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now.AddMinutes(40));

        Assert.False(afterRestart);
        Assert.Equal(10, _db.FixtureRefreshAttempts.AsNoTracking().Sum(a => a.AttemptCount));
    }

    [Fact]
    public void GunlukTavan_ErtesiGunSifirlanir()
    {
        var repo = Repo();
        for (var i = 0; i < 10; i++)
            repo.TryReserveFixtureAttempt($"fx{i}", FixtureRefreshPurposes.Result,
                TimeSpan.FromHours(12), 10, Now);

        var nextDay = RestartedProcess().TryReserveFixtureAttempt(
            "fx99", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now.AddDays(1));

        Assert.True(nextDay);
    }

    [Fact]
    public void IkiSurec_AyniFikstruAyniAndaALAMAZ()
    {
        // İki ayrı süreç (ayrı bağlam), aynı fikstür, aynı an.
        var a = RestartedProcess();
        var b = RestartedProcess();

        var firstWins = a.TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now);
        var secondWins = b.TryReserveFixtureAttempt(
            "1584394", FixtureRefreshPurposes.Result, TimeSpan.FromHours(12), 10, Now);

        // Tam olarak BİRİ kazanır — ikisi birden istek yapamaz.
        Assert.True(firstWins ^ secondWins);
    }

    [Fact]
    public void IkiAmac_AyriButceHavuzuKullanir()
    {
        var repo = Repo();
        for (var i = 0; i < 10; i++)
            repo.TryReserveFixtureAttempt($"r{i}", FixtureRefreshPurposes.Result,
                TimeSpan.FromHours(12), 10, Now);

        // Sonuç havuzu doldu ama GELECEK TAKVİM havuzu ayrıdır.
        var future = RestartedProcess().TryReserveFixtureAttempt(
            "f1", FixtureRefreshPurposes.FutureSchedule, TimeSpan.FromHours(24), 5, Now);

        Assert.True(future);
    }

    [Fact]
    public void GelecekTakvim_GunlukTavani_RestartSonrasiKorunur()
    {
        var repo = Repo();
        for (var i = 0; i < 5; i++)
            Assert.True(repo.TryReserveFixtureAttempt(
                $"f{i}", FixtureRefreshPurposes.FutureSchedule, TimeSpan.FromHours(24), 5, Now));

        var afterRestart = RestartedProcess().TryReserveFixtureAttempt(
            "f99", FixtureRefreshPurposes.FutureSchedule, TimeSpan.FromHours(24), 5, Now.AddMinutes(30));

        Assert.False(afterRestart);
    }

    public void Dispose() => _db.Dispose();
}
