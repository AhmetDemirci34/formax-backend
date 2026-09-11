using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Interfaces;
using Formax.Application.UseCases.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// TAHMİNLERİM OKUMA YOLU — kalıcı settlement alanlarını OLDUĞU GİBİ okur.
///
/// ÖLÇÜLDÜ (11.09.2026): <see cref="GetUserPredictionsUseCase"/> her GET'te sonucu
/// <c>PickSettlement.Settle</c> ile yeniden hesaplıyor, sonuçlandırma işinin DB'ye
/// yazdığı <c>Status</c>/<c>SettledAtUtc</c>/<c>SettlementNote</c> alanlarını hiç
/// okumuyordu. Bu testler okuma yolunun artık yalnız kalıcı kaydı yansıttığını,
/// hiçbir şey hesaplamadığını ve hiçbir şey yazmadığını sınar.
///
/// Gerçek ağ YOK; EF Core InMemory + GERÇEK repository uygulamaları.
/// </summary>
public class UserPredictionsReadPathTests
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";
    private const int LeagueId = 203;

    private static readonly DateTime Now = new(2026, 9, 11, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastKickoff = Now.AddDays(-1);
    private static readonly DateTime FutureKickoff = Now.AddDays(2);

    /// <summary>Kalıcı sonuçlandırma anı — "şimdi"den bilerek farklı.</summary>
    private static readonly DateTime SettledAt = new(2026, 9, 10, 21, 37, 12, DateTimeKind.Utc);

    private readonly string _dbName = $"picks-read-{Guid.NewGuid():N}";

    /// <summary>Okuma bağlamındaki HER SaveChanges denemesini sayar.</summary>
    private sealed class WriteCounter : SaveChangesInterceptor
    {
        public int Writes;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref Writes);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Writes);
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private FormaxDbContext NewContext(WriteCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (counter != null) builder.AddInterceptors(counter);
        return new FormaxDbContext(builder.Options);
    }

    private static GetUserPredictionsUseCase UseCase(FormaxDbContext db)
        => new(new UserPickRepository(db),
               new MatchReadRepository(db, new ConfigurationBuilder().Build()));

    private void Seed(params object[] rows)
    {
        using var db = NewContext();
        if (!db.Teams.Any())
        {
            db.Teams.Add(new Team { Id = 1, Name = "Ev Sahibi" });
            db.Teams.Add(new Team { Id = 2, Name = "Deplasman" });
        }
        foreach (var r in rows)
        {
            switch (r)
            {
                case Match m: db.Matches.Add(m); break;
                case UserPick p: db.UserPicks.Add(p); break;
            }
        }
        db.SaveChanges();
    }

    private static Match FinishedMatch(int id, int home, int away, int? htHome = null, int? htAway = null) => new()
    {
        Id = id, Status = MatchStatuses.Finished, MatchDate = PastKickoff,
        HomeTeamId = 1, AwayTeamId = 2, HomeScore = home, AwayScore = away,
        HalfTimeHomeScore = htHome, HalfTimeAwayScore = htAway,
        LeagueId = LeagueId, League = "Süper Lig"
    };

    private static Match OpenMatch(int id, string status, DateTime kickoff) => new()
    {
        Id = id, Status = status, MatchDate = kickoff,
        HomeTeamId = 1, AwayTeamId = 2, LeagueId = LeagueId, League = "Süper Lig"
    };

    private static UserPick Pick(
        string userId, int matchId, string marketKey, string label, int probability,
        PickStatus status = PickStatus.Pending, string? selectionStatus = PickSelectionStatuses.Active,
        DateTime? settledAt = null, string? note = null) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, MatchId = matchId,
        MarketKey = marketKey, PickLabel = label,
        Confidence = probability, ProbabilityPercent = probability,
        CreatedAt = PastKickoff.AddHours(-3), MatchKickoffUtc = PastKickoff,
        Status = status, SelectionStatus = selectionStatus,
        SettledAtUtc = settledAt, SettlementNote = note
    };

    // ── 1. KALICI DOĞRU SONUÇ GET İLE AYNEN DÖNER ───────────────────────────────
    [Fact]
    public async Task KaliciDogruSonuc_GetIleAynenDoner()
    {
        var pick = Pick(UserA, 500, OddsMarketKeys.Ms1, "Ev Sahibi Kazanır", 62,
            PickStatus.Win, PickSelectionStatuses.Settled, SettledAt, "MS 2-1");
        Seed(FinishedMatch(500, 2, 1, 1, 0), pick);

        using var db = NewContext();
        var card = Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now));
        var s = Assert.Single(card.Selections);

        Assert.Equal(PickSelectionStatuses.Settled, s.SelectionStatus);
        Assert.True(s.IsCorrect);
        Assert.Equal("MS 2-1", s.SettlementNote);
        Assert.Equal(PickSelectionStatuses.Settled, card.CardStatus);      // "Tamamlandı"

        // KULLANICININ SEÇİMİ: etiket ve seçim anındaki yüzde kayıttan aynen gelir.
        Assert.Equal("Ev Sahibi Kazanır", s.Label);
        Assert.Equal(62, s.ProbabilityPercent);

        // İY / 2Y / MS maçın gerçek skorundan.
        Assert.Equal((1, 0), (card.HalfTimeHomeScore, card.HalfTimeAwayScore));
        Assert.Equal((1, 1), (card.SecondHalfHomeScore, card.SecondHalfAwayScore));
        Assert.Equal((2, 1), (card.HomeScore, card.AwayScore));
    }

    // ── 2. KALICI YANLIŞ SONUÇ GET İLE AYNEN DÖNER ──────────────────────────────
    [Fact]
    public async Task KaliciYanlisSonuc_GetIleAynenDoner()
    {
        var pick = Pick(UserA, 501, OddsMarketKeys.Under25, "2.5 Alt", 55,
            PickStatus.Lose, PickSelectionStatuses.Settled, SettledAt, "MS 2-1");
        Seed(FinishedMatch(501, 2, 1), pick);

        using var db = NewContext();
        var s = Assert.Single(Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now)).Selections);

        Assert.Equal(PickSelectionStatuses.Settled, s.SelectionStatus);
        Assert.False(s.IsCorrect);
        Assert.Equal("MS 2-1", s.SettlementNote);
    }

    // ── 3. SettledAtUtc AYNEN KORUNUR ───────────────────────────────────────────
    [Fact]
    public async Task SettledAtUtc_DbdenAynenGelir()
    {
        var pick = Pick(UserA, 502, OddsMarketKeys.BttsYes, "Karşılıklı Gol Var", 58,
            PickStatus.Win, PickSelectionStatuses.Settled, SettledAt, "MS 2-1");
        Seed(FinishedMatch(502, 2, 1), pick);

        using (var db = NewContext())
        {
            var s = Assert.Single(Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now)).Selections);
            Assert.Equal(SettledAt, s.SettledAtUtc);
        }

        // Okuma satırın zaman damgasını "şimdi"ye kaydırmaz.
        using var check = NewContext();
        Assert.Equal(SettledAt, check.UserPicks.AsNoTracking().Single(p => p.Id == pick.Id).SettledAtUtc);
    }

    // ── 4. GET SETTLEMENT SERVİSİNİ ÇALIŞTIRMAZ ─────────────────────────────────
    [Fact]
    public async Task Get_SettlementCalistirmaz_SonuclanmamisSecimBekleyenKalir()
    {
        // Maç BİTTİ, market DESTEKLENİYOR (2-1 → MS1 tutar) ama iş henüz geçmedi.
        var pick = Pick(UserA, 503, OddsMarketKeys.Ms1, "Ev Sahibi Kazanır", 60);
        Seed(FinishedMatch(503, 2, 1), pick);

        using (var db = NewContext())
        {
            var card = Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now));
            var s = Assert.Single(card.Selections);

            // Okuma yolu sonucu TAHMİN EDİP doldurmaz.
            Assert.Equal(PickSelectionStatuses.Pending, s.SelectionStatus);
            Assert.Null(s.IsCorrect);
            Assert.Null(s.SettledAtUtc);
            Assert.Equal(PickSelectionStatuses.Pending, card.CardStatus);   // "Bekleyen"
        }

        // DB satırı sonuçlandırılmadı.
        using var check = NewContext();
        var row = check.UserPicks.AsNoTracking().Single(p => p.Id == pick.Id);
        Assert.Equal(PickStatus.Pending, row.Status);
        Assert.Null(row.SettledAtUtc);
        Assert.Equal(PickSelectionStatuses.Active, row.SelectionStatus);

        // Okuma use case'inin sonuçlandırıcıya bağımlılığı YOKTUR: yalnız iki okuyucu.
        var ctorParams = typeof(GetUserPredictionsUseCase).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Equal(new[] { typeof(IUserPickRepository), typeof(IMatchReadRepository) }, ctorParams);
    }

    // ── 5. GET DB'DE WRITE ÜRETMEZ ──────────────────────────────────────────────
    [Fact]
    public async Task Get_DbdeWriteUretmez()
    {
        Seed(FinishedMatch(504, 0, 0),
            Pick(UserA, 504, OddsMarketKeys.MsX, "Beraberlik", 30,
                PickStatus.Win, PickSelectionStatuses.Settled, SettledAt, "MS 0-0"),
            Pick(UserA, 504, OddsMarketKeys.FirstGoalHome, "İlk Gol Ev Sahibi", 40,
                PickStatus.Pending, PickSelectionStatuses.Unsettleable),
            Pick(UserA, 504, OddsMarketKeys.Over25, "2.5 Üst", 45));

        var counter = new WriteCounter();
        using var db = NewContext(counter);
        await UseCase(db).ExecuteAsync(UserA, Now);

        Assert.Equal(0, counter.Writes);
        Assert.Empty(db.ChangeTracker.Entries());   // izlenen/değişmiş satır YOK
    }

    // ── 6. AYNI GET İKİ KEZ ÇAĞRILDIĞINDA SONUÇ DEĞİŞMEZ ────────────────────────
    [Fact]
    public async Task AyniGet_IkiKez_AyniSonuc()
    {
        Seed(FinishedMatch(505, 1, 2),
            Pick(UserA, 505, OddsMarketKeys.Ms2, "Deplasman Kazanır", 35,
                PickStatus.Win, PickSelectionStatuses.Settled, SettledAt, "MS 1-2"),
            Pick(UserA, 505, OddsMarketKeys.Ht1, "İY Ev Sahibi", 33,
                PickStatus.Pending, PickSelectionStatuses.Unsettleable),
            OpenMatch(506, MatchStatuses.NotStarted, FutureKickoff),
            Pick(UserA, 506, OddsMarketKeys.Under25, "2.5 Alt", 52));

        string first, second;
        using (var db = NewContext()) first = JsonSerializer.Serialize(await UseCase(db).ExecuteAsync(UserA, Now));
        using (var db = NewContext()) second = JsonSerializer.Serialize(await UseCase(db).ExecuteAsync(UserA, Now));

        Assert.Equal(first, second);
    }

    // ── 7. DESTEKLENMEYEN MARKET PENDING KALIR ──────────────────────────────────
    [Fact]
    public async Task DesteklenmeyenMarket_PendingKalir_SonucHesaplanamadi()
    {
        var pick = Pick(UserA, 507, OddsMarketKeys.FirstGoalHome, "İlk Gol Ev Sahibi", 44,
            PickStatus.Pending, PickSelectionStatuses.Unsettleable);
        Seed(FinishedMatch(507, 2, 1, 1, 0), pick);

        using (var db = NewContext())
        {
            var card = Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now));
            var s = Assert.Single(card.Selections);

            Assert.Equal(PickSelectionStatuses.Unsettleable, s.SelectionStatus);   // "Sonuç hesaplanamadı"
            Assert.Null(s.IsCorrect);
            Assert.Null(s.SettledAtUtc);
            // Kalıcı son durum yazılmış tek seçim: kart kapanmış sayılır.
            Assert.Equal(PickSelectionStatuses.Settled, card.CardStatus);
        }

        using var check = NewContext();
        var row = check.UserPicks.AsNoTracking().Single(p => p.Id == pick.Id);
        Assert.Equal(PickStatus.Pending, row.Status);
        Assert.Null(row.SettledAtUtc);
    }

    // ── 8. BİTMEMİŞ MAÇ BEKLEYEN KALIR ──────────────────────────────────────────
    [Fact]
    public async Task BitmemisMac_BekleyenKalir_BaslamamisMacAktif()
    {
        Seed(OpenMatch(508, MatchStatuses.Live, PastKickoff.AddDays(1).AddMinutes(-30)),
            Pick(UserA, 508, OddsMarketKeys.Over25, "2.5 Üst", 57),
            OpenMatch(509, MatchStatuses.NotStarted, FutureKickoff),
            Pick(UserA, 509, OddsMarketKeys.BttsNo, "Karşılıklı Gol Yok", 49));

        using var db = NewContext();
        var cards = await UseCase(db).ExecuteAsync(UserA, Now);

        var live = cards.Single(c => c.MatchId == 508);
        Assert.Equal(PickSelectionStatuses.Pending, live.CardStatus);                 // "Bekleyen"
        Assert.Equal(PickSelectionStatuses.Pending, Assert.Single(live.Selections).SelectionStatus);
        Assert.Null(live.HomeScore);                                                   // skor uydurulmaz

        var upcoming = cards.Single(c => c.MatchId == 509);
        Assert.Equal(PickSelectionStatuses.Active, upcoming.CardStatus);
        Assert.Null(Assert.Single(upcoming.Selections).IsCorrect);
    }

    // ── 9. ESKİ YENİDEN HESAPLAMA YOLU KULLANILMAZ ──────────────────────────────
    /// <summary>
    /// Kalıcı karar, bugünkü kuralın vereceği kararla ÇELİŞSE bile kayıt kazanır.
    /// Senaryo: seçim eski skorla (1-1) sonuçlandırıldı ve "yanlış" yazıldı; maçın
    /// skoru sonradan 2-1'e düzeltildi. Yeniden hesaplama MS1'i "doğru" yapardı —
    /// tamamlanmış seçim sonradan değişen koddan/veriden ETKİLENMEZ.
    /// </summary>
    [Fact]
    public async Task EskiYenidenHesaplamaYolu_Kullanilmaz_KaliciKararKazanir()
    {
        Seed(FinishedMatch(510, 2, 1),
            Pick(UserA, 510, OddsMarketKeys.Ms1, "Ev Sahibi Kazanır", 61,
                PickStatus.Lose, PickSelectionStatuses.Settled, SettledAt, "MS 1-1"));

        using var db = NewContext();
        var s = Assert.Single(Assert.Single(await UseCase(db).ExecuteAsync(UserA, Now)).Selections);

        Assert.False(s.IsCorrect);                  // yeniden hesaplansaydı true olurdu
        Assert.Equal("MS 1-1", s.SettlementNote);   // not da kayıttan, bugünkü skordan değil
        Assert.Equal(SettledAt, s.SettledAtUtc);
    }

    // ── 10. KULLANICININ YALNIZ KENDİ SEÇİMLERİ DÖNER ───────────────────────────
    [Fact]
    public async Task YalnizKendiSecimleriDoner()
    {
        var mine = Pick(UserA, 511, OddsMarketKeys.Ms1, "Ev Sahibi Kazanır", 60,
            PickStatus.Win, PickSelectionStatuses.Settled, SettledAt, "MS 2-0");
        var theirs = Pick(UserB, 511, OddsMarketKeys.Ms2, "Deplasman Kazanır", 20,
            PickStatus.Lose, PickSelectionStatuses.Settled, SettledAt, "MS 2-0");
        var theirsOnly = Pick(UserB, 512, OddsMarketKeys.Over25, "2.5 Üst", 50);
        Seed(FinishedMatch(511, 2, 0), OpenMatch(512, MatchStatuses.NotStarted, FutureKickoff),
            mine, theirs, theirsOnly);

        using var db = NewContext();
        var cards = await UseCase(db).ExecuteAsync(UserA, Now);

        var card = Assert.Single(cards);                         // B'nin 512 kartı YOK
        Assert.Equal(511, card.MatchId);
        Assert.Equal(mine.Id, Assert.Single(card.Selections).Id); // B'nin 511 seçimi YOK
    }
}
