using System;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.UseCases.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Picks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// SEÇİM SONUÇLANDIRMA — GERÇEK KALICILIK KATMANIYLA.
///
/// NEDEN BELLEK İÇİ DEPO YETMEZ: sonuçlandırmanın sorusu "hesap doğru mu" değil,
/// "sonuç GERÇEKTEN yazıldı mı"dır. Bellek içi sağlayıcı, gerçek şemanın kısıtlarını
/// (kolon türü, zorunluluk, benzersiz indeks) ve bir sonraki okumada satırın geri
/// gelip gelmediğini KANITLAMAZ. Bu test gerçek SQL Server şemasına yazar ve aynı
/// satırı yeni bir bağlamdan geri okur.
///
/// ÜRETİM VERİSİNE DOKUNULMAZ:
///  • Kullanıcı, maç ve seçimler bu test tarafından YARATILIR (kendi kimlikleriyle).
///  • Hiçbir mevcut satır güncellenmez ya da silinmez.
///  • Test sonunda YALNIZ kendi yarattığı satırlar silinir (<see cref="DisposeAsync"/>),
///    hata durumunda bile.
///
/// SQL Server erişilebilir değilse test ATLANIR — sessizce "geçti" demez.
/// </summary>
public class UserPickSettlementIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>Bu koşuya ait benzersiz test kullanıcısı — üretim kullanıcısı DEĞİL.</summary>
    private readonly string _testUserId = "test-settlement-" + Guid.NewGuid().ToString("N")[..12];

    private FormaxDbContext? _db;
    private int _matchId;
    private int _homeTeamId, _awayTeamId;
    private bool _available;

    private static DbContextOptions<FormaxDbContext> Options() =>
        new DbContextOptionsBuilder<FormaxDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

    public async Task InitializeAsync()
    {
        try
        {
            _db = new FormaxDbContext(Options());
            _available = await _db.Database.CanConnectAsync();
        }
        catch (SqlException) { _available = false; }
        catch (InvalidOperationException) { _available = false; }

        if (!_available) return;

        // ── TEST VERİSİ ───────────────────────────────────────────────────────
        // Var olan takımlar YENİDEN KULLANILIR (yabancı anahtar için); değiştirilmez.
        var teams = await _db!.Teams.AsNoTracking().OrderBy(t => t.Id).Take(2).ToListAsync();
        if (teams.Count < 2) { _available = false; return; }
        _homeTeamId = teams[0].Id;
        _awayTeamId = teams[1].Id;

        var kickoff = DateTime.UtcNow.AddDays(-1);

        // BİTMİŞ test maçı: 2-1 (MS 1 kazanır, KG VAR tutar, 2.5 ÜST tutar).
        var match = new Match
        {
            Status = MatchStatuses.Finished,
            MatchDate = kickoff,
            HomeTeamId = _homeTeamId,
            AwayTeamId = _awayTeamId,
            HomeScore = 2,
            AwayScore = 1,
            LeagueId = 203,
            League = "TEST-SETTLEMENT",
            ExternalMatchId = "test-settle-" + Guid.NewGuid().ToString("N")[..10]
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();
        _matchId = match.Id;

        // SEÇİMLER KICKOFF ÖNCESİNDE kaydedilmiş sayılır.
        var createdAt = kickoff.AddHours(-3);

        _db.UserPicks.AddRange(
            // (1) DESTEKLENEN market — doğru seçim.
            new UserPick
            {
                Id = Guid.NewGuid(), UserId = _testUserId, MatchId = _matchId,
                PickLabel = "Ev sahibi kazanır", MarketKey = OddsMarketKeys.Ms1,
                Confidence = 60, CreatedAt = createdAt, MatchKickoffUtc = kickoff,
                Status = PickStatus.Pending
            },
            // (2) DESTEKLENEN market — yanlış seçim.
            new UserPick
            {
                Id = Guid.NewGuid(), UserId = _testUserId, MatchId = _matchId,
                PickLabel = "2,5 Alt", MarketKey = OddsMarketKeys.Under25,
                Confidence = 50, CreatedAt = createdAt, MatchKickoffUtc = kickoff,
                Status = PickStatus.Pending
            },
            // (3) DESTEKLENMEYEN market — skordan TÜRETİLEMEZ.
            new UserPick
            {
                Id = Guid.NewGuid(), UserId = _testUserId, MatchId = _matchId,
                PickLabel = "İlk golü ev sahibi atar", MarketKey = "first_goal_home",
                Confidence = 55, CreatedAt = createdAt, MatchKickoffUtc = kickoff,
                Status = PickStatus.Pending
            });

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [SkippableFact]
    public async Task SettlementIsi_GercekDepoyaYazar_UydurmaSonucUretmez()
    {
        Skip.IfNot(_available, "SQL Server (localhost\\SQLEXPRESS/FormaxDB) erisilebilir degil.");

        var service = new UserPickSettlementService(
            _db!, NullLogger<UserPickSettlementService>.Instance);

        var result = await service.RunCycleAsync(_matchId);

        Assert.Equal(3, result.Examined);
        Assert.Equal(2, result.Settled);          // yalnız desteklenen iki market
        Assert.Equal(1, result.Unsettleable);

        // ── KALICILIK: YENİ bir bağlamdan geri okunur ─────────────────────────
        // Aynı bağlamın önbelleğinden okumak, yazmanın gerçekten diske indiğini
        // kanıtlamazdı.
        using var fresh = new FormaxDbContext(Options());
        var picks = await fresh.UserPicks.AsNoTracking()
            .Where(p => p.UserId == _testUserId)
            .ToListAsync();

        var ms1 = picks.Single(p => p.MarketKey == OddsMarketKeys.Ms1);
        Assert.Equal(PickStatus.Win, ms1.Status);
        Assert.Equal(PickSelectionStatuses.Settled, ms1.SelectionStatus);
        Assert.NotNull(ms1.SettledAtUtc);
        Assert.Equal("MS 2-1", ms1.SettlementNote);

        var under = picks.Single(p => p.MarketKey == OddsMarketKeys.Under25);
        Assert.Equal(PickStatus.Lose, under.Status);
        Assert.Equal(PickSelectionStatuses.Settled, under.SelectionStatus);

        // DESTEKLENMEYEN MARKET: uydurma sonuç YOK.
        var firstGoal = picks.Single(p => p.MarketKey == "first_goal_home");
        Assert.Equal(PickStatus.Pending, firstGoal.Status);
        Assert.Null(firstGoal.SettledAtUtc);
        Assert.Equal(PickSelectionStatuses.Unsettleable, firstGoal.SelectionStatus);
    }

    [SkippableFact]
    public async Task TahminlerimDtosu_KaliciSonucuOkur_GetHesaplamazVeYazmaz()
    {
        Skip.IfNot(_available, "SQL Server (localhost\\SQLEXPRESS/FormaxDB) erisilebilir degil.");

        // GERÇEK repository uygulamaları — test için sahte okuyucu YAZILMADI.
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        GetUserPredictionsUseCase UseCase(FormaxDbContext db) => new(
            new UserPickRepository(db),
            new Formax.Infrastructure.Repositories.MatchReadRepository(db, config));

        // ── (1) SONUÇLANDIRMA ÖNCESİ: maç bitmiş ama iş geçmedi → "Bekleyen",
        //        GET sonucu hesaplamaz ve DB'ye hiçbir şey yazmaz.
        using (var before = new FormaxDbContext(Options()))
        {
            var pending = (await UseCase(before).ExecuteAsync(_testUserId, DateTime.UtcNow))
                .Single(c => c.MatchId == _matchId);
            Assert.Equal(PickSelectionStatuses.Pending, pending.CardStatus);
            Assert.All(pending.Selections, s =>
            {
                Assert.Equal(PickSelectionStatuses.Pending, s.SelectionStatus);
                Assert.Null(s.IsCorrect);
                Assert.Null(s.SettledAtUtc);
            });
            Assert.Empty(before.ChangeTracker.Entries());
        }
        using (var check = new FormaxDbContext(Options()))
        {
            Assert.All(await check.UserPicks.AsNoTracking().Where(p => p.UserId == _testUserId).ToListAsync(),
                p => { Assert.Null(p.SettledAtUtc); Assert.Equal(PickStatus.Pending, p.Status); });
        }

        // ── (2) SONUÇLANDIRMA — tek yazıcı, DB'ye kalıcı yazar.
        await new UserPickSettlementService(_db!, NullLogger<UserPickSettlementService>.Instance)
            .RunCycleAsync(_matchId);

        using var fresh = new FormaxDbContext(Options());
        var persisted = await fresh.UserPicks.AsNoTracking()
            .Where(p => p.UserId == _testUserId).ToListAsync();

        // ── (3) SONRAKİ TAHMİNLERİM SORGUSU — kalıcı sonucu AYNEN okur.
        var cards = await UseCase(fresh).ExecuteAsync(_testUserId, DateTime.UtcNow);
        var card = cards.Single(c => c.MatchId == _matchId);

        Assert.Equal(PickSelectionStatuses.Settled, card.CardStatus);   // "Tamamlandı"
        Assert.Equal(2, card.HomeScore);
        Assert.Equal(1, card.AwayScore);

        var ms1 = card.Selections.Single(s => s.MarketKey == OddsMarketKeys.Ms1);
        var ms1Row = persisted.Single(p => p.MarketKey == OddsMarketKeys.Ms1);
        Assert.Equal(PickSelectionStatuses.Settled, ms1.SelectionStatus);
        Assert.True(ms1.IsCorrect);
        Assert.Equal(ms1Row.SettledAtUtc, ms1.SettledAtUtc);            // DB'den, "şimdi"den değil
        Assert.Equal(ms1Row.SettlementNote, ms1.SettlementNote);

        var under = card.Selections.Single(s => s.MarketKey == OddsMarketKeys.Under25);
        Assert.False(under.IsCorrect);
        Assert.Equal(persisted.Single(p => p.MarketKey == OddsMarketKeys.Under25).SettledAtUtc, under.SettledAtUtc);

        // Hesaplanamayan seçim doğru/yanlış İDDİA ETMEZ.
        var firstGoal = card.Selections.Single(s => s.MarketKey == "first_goal_home");
        Assert.Equal(PickSelectionStatuses.Unsettleable, firstGoal.SelectionStatus);
        Assert.Null(firstGoal.IsCorrect);
        Assert.Null(firstGoal.SettledAtUtc);

        // ── (4) İKİNCİ OKUMA aynı sonucu verir ve satırları değiştirmez.
        using var again = new FormaxDbContext(Options());
        var second = (await UseCase(again).ExecuteAsync(_testUserId, DateTime.UtcNow))
            .Single(c => c.MatchId == _matchId);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(card),
            System.Text.Json.JsonSerializer.Serialize(second));

        var after = await again.UserPicks.AsNoTracking().Where(p => p.UserId == _testUserId).ToListAsync();
        foreach (var row in persisted)
        {
            var now = after.Single(p => p.Id == row.Id);
            Assert.Equal(row.Status, now.Status);
            Assert.Equal(row.SettledAtUtc, now.SettledAtUtc);
            Assert.Equal(row.SelectionStatus, now.SelectionStatus);
            Assert.Equal(row.SettlementNote, now.SettlementNote);
        }
    }

    /// <summary>
    /// TEMİZLİK — yalnız bu testin yarattığı satırlar. Mevcut kullanıcı kayıtlarına
    /// ve mevcut maçlara DOKUNULMAZ.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_db == null) return;

        try
        {
            if (_available)
            {
                var picks = await _db.UserPicks.Where(p => p.UserId == _testUserId).ToListAsync();
                if (picks.Count > 0) _db.UserPicks.RemoveRange(picks);

                if (_matchId != 0)
                {
                    var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == _matchId);
                    if (match != null) _db.Matches.Remove(match);
                }

                await _db.SaveChangesAsync();
            }
        }
        finally
        {
            await _db.DisposeAsync();
        }
    }
}
