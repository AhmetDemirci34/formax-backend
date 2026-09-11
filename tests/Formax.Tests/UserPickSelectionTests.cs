using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Picks;
using Formax.Application.UseCases.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// "SENİN SEÇİMİN" — kullanıcının olası sonuç seçimleri.
///
/// Gerçek ağ ve gerçek veritabanı KULLANILMAZ: bellek içi sahte repository ile
/// yalnız sözleşme sınanır.
/// </summary>
public class UserPickSelectionTests
{
    private const string User = "42";
    private const int MatchId = 103620;                                   // Trabzonspor–Gençlerbirliği
    private static readonly DateTime Kickoff = new(2026, 9, 6, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BeforeKickoff = Kickoff.AddHours(-3);
    private static readonly DateTime AfterKickoff = Kickoff.AddMinutes(10);

    // ── 34. SEÇİM KAYDEDİLİR ────────────────────────────────────────────────────
    [Fact]
    public async Task Secim_Kaydedilir()
    {
        var (useCase, repo) = Build();

        var r = await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);

        Assert.True(r.Accepted);
        Assert.Single(r.Selections);
        Assert.Equal(OddsMarketKeys.Under25, r.Selections[0].MarketKey);
        Assert.Equal(61, r.Selections[0].ProbabilityPercent);
        Assert.Single(repo.Rows);

        // MODEL ANLIK GÖRÜNTÜSÜ dondurulur: seçim anındaki yüzde ve kickoff saklanır.
        Assert.Equal(Kickoff, repo.Rows[0].MatchKickoffUtc);
        Assert.Equal(PickSelectionStatuses.Active, repo.Rows[0].SelectionStatus);
    }

    [Fact]
    public async Task BilinmeyenMarket_Reddedilir()
    {
        var (useCase, repo) = Build();

        var r = await useCase.ToggleAsync(User, Req("Uydurma Market", 99), BeforeKickoff);

        Assert.False(r.Accepted);
        Assert.Equal("UNKNOWN_MARKET", r.RejectionReason);
        Assert.Empty(repo.Rows);
    }

    // ── 35. DUPLICATE SEÇİM OLUŞMAZ (ikinci basış KALDIRIR) ─────────────────────
    [Fact]
    public async Task AyniSecim_IkinciBasista_Kaldirilir()
    {
        var (useCase, repo) = Build();

        await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);
        var second = await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);

        Assert.True(second.Accepted);
        Assert.Empty(second.Selections);
        Assert.Empty(repo.Rows);          // duplicate satır YOK
    }

    // ── 36. AYNI MARKETTE ÇELİŞKİLİ SEÇİM DEĞİŞTİRİLİR ──────────────────────────
    [Theory]
    [InlineData("2.5 Alt", "2.5 Üst")]
    [InlineData("Karşılıklı Gol Var", "Karşılıklı Gol Yok")]
    [InlineData("Çifte Şans (1X)", "Çifte Şans (X2)")]
    [InlineData("Ev Sahibi Kazanır", "Beraberlik")]
    [InlineData("İlk Gol Ev Sahibi", "İlk Gol Deplasman")]
    public async Task CeliskiliSecim_Degistirilir(string first, string second)
    {
        var (useCase, repo) = Build();

        await useCase.ToggleAsync(User, Req(first, 55), BeforeKickoff);
        var r = await useCase.ToggleAsync(User, Req(second, 45), BeforeKickoff);

        Assert.True(r.Accepted);
        Assert.Single(r.Selections);
        Assert.Equal(second, r.Selections[0].Label);
        Assert.Single(repo.Rows);
    }

    // ── 37. FARKLI MARKET SEÇİMLERİ BİRLİKTE KALIR ──────────────────────────────
    [Fact]
    public async Task FarkliMarketler_BirlikteKalir()
    {
        var (useCase, repo) = Build();

        await useCase.ToggleAsync(User, Req("Çifte Şans (1X)", 72), BeforeKickoff);
        await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);
        var r = await useCase.ToggleAsync(User, Req("Karşılıklı Gol Var", 54), BeforeKickoff);

        Assert.True(r.Accepted);
        Assert.Equal(3, r.Selections.Count);
        Assert.Equal(3, repo.Rows.Count);

        var groups = r.Selections.Select(s => s.MarketGroup).ToList();
        Assert.Equal(3, groups.Distinct().Count());   // üç AYRI grup
    }

    // ── 38. MAÇ BAŞLADIKTAN SONRA SEÇİM REDDEDİLİR ──────────────────────────────
    [Fact]
    public async Task MacBasladiktanSonra_YeniSecimReddedilir()
    {
        var (useCase, repo) = Build();

        var r = await useCase.ToggleAsync(User, Req("2.5 Alt", 61), AfterKickoff);

        Assert.False(r.Accepted);
        Assert.Equal("MATCH_ALREADY_STARTED", r.RejectionReason);
        Assert.Empty(repo.Rows);
    }

    [Fact]
    public async Task BitmisMacta_YeniSecimReddedilir()
    {
        var (useCase, repo) = Build(status: MatchStatuses.Finished);

        var r = await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);

        Assert.False(r.Accepted);
        Assert.Equal("MATCH_ALREADY_STARTED", r.RejectionReason);
        Assert.Empty(repo.Rows);
    }

    [Fact]
    public async Task MacBasladiktanSonra_MevcutSecimKaldirilabilir()
    {
        // Kullanıcı kendi kaydını her zaman silebilmelidir; kısıt YENİ seçime aittir.
        var (useCase, repo) = Build();
        await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);

        var r = await useCase.ToggleAsync(User, Req("2.5 Alt", 61), AfterKickoff);

        Assert.True(r.Accepted);
        Assert.Empty(repo.Rows);
    }

    // ── 39. SAYFA YENİLENİNCE SEÇİM KORUNUR ─────────────────────────────────────
    [Fact]
    public async Task SayfaYenilenince_SecimKorunur()
    {
        var (useCase, _) = Build();
        await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);
        await useCase.ToggleAsync(User, Req("Karşılıklı Gol Var", 54), BeforeKickoff);

        // "Yenileme" = durumun yeniden okunması. Kaynak DB'dir, tarayıcı belleği değil.
        var restored = await useCase.GetForMatchAsync(User, MatchId);

        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, s => s.MarketKey == OddsMarketKeys.Under25);
        Assert.Contains(restored, s => s.MarketKey == OddsMarketKeys.BttsYes);
    }

    [Fact]
    public async Task BaskaKullanicininSecimi_Gorunmez()
    {
        var (useCase, _) = Build();
        await useCase.ToggleAsync(User, Req("2.5 Alt", 61), BeforeKickoff);

        Assert.Empty(await useCase.GetForMatchAsync("999", MatchId));
    }

    // ── ÇAKIŞMA KURALI (saf) ────────────────────────────────────────────────────
    [Fact]
    public void CakismaKurali_AyniGrupCeliskilidir()
    {
        Assert.True(PickMarketGroups.Conflicts(OddsMarketKeys.Under25, OddsMarketKeys.Over25));
        Assert.True(PickMarketGroups.Conflicts(OddsMarketKeys.BttsYes, OddsMarketKeys.BttsNo));
        Assert.True(PickMarketGroups.Conflicts(OddsMarketKeys.DoubleChance1X, OddsMarketKeys.DoubleChanceX2));
        Assert.True(PickMarketGroups.Conflicts(OddsMarketKeys.Ms1, OddsMarketKeys.DoubleChanceX2));

        // FARKLI GRUPLAR ÇELİŞMEZ.
        Assert.False(PickMarketGroups.Conflicts(OddsMarketKeys.Under25, OddsMarketKeys.BttsYes));
        Assert.False(PickMarketGroups.Conflicts(OddsMarketKeys.Ms1, OddsMarketKeys.Over25));
        Assert.False(PickMarketGroups.Conflicts(OddsMarketKeys.Ht1, OddsMarketKeys.Ms1));

        // Kendisiyle çelişmez; bilinmeyen anahtar hiçbir şeyi dışlamaz.
        Assert.False(PickMarketGroups.Conflicts(OddsMarketKeys.Ms1, OddsMarketKeys.Ms1));
        Assert.False(PickMarketGroups.Conflicts("BILINMEYEN", OddsMarketKeys.Ms1));
        Assert.Null(PickMarketGroups.GroupOf("BILINMEYEN"));
    }

    // ── 42. SETTLEMENT YALNIZ DESTEKLENEN MARKETTE ──────────────────────────────
    [Fact]
    public void Settlement_DesteklenenMarketlerdeHesaplanir()
    {
        // Çorum FK 0 - 1 Kasımpaşa (gerçek sonuç, MatchId 99156)
        var s = new PickSettlement.FinalScore(0, 1, null, null);

        Assert.Equal(PickSettlement.PickSettlementOutcome.Lost,
            PickSettlement.Settle(OddsMarketKeys.Ms1, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.Ms2, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.Under25, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Lost,
            PickSettlement.Settle(OddsMarketKeys.Over25, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Lost,
            PickSettlement.Settle(OddsMarketKeys.BttsYes, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.BttsNo, s));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.DoubleChanceX2, s));
    }

    /// <summary>
    /// HESAPLANAMAYAN MARKETTE UYDURMA SONUÇ YOK.
    ///
    /// İlk yarı marketleri yalnız İY skoru GERÇEKTEN varsa hesaplanır; null iken
    /// 0-0 varsaymak, olmayan veriden sonuç üretmek olurdu. "İlk golü kim attı"
    /// ise skordan türetilemez.
    /// </summary>
    [Fact]
    public void Settlement_HesaplanamayanMarkette_UydurmaYok()
    {
        var noHalfTime = new PickSettlement.FinalScore(2, 1, null, null);

        Assert.False(PickSettlement.IsSupported(OddsMarketKeys.Ht1, noHalfTime));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Unsettleable,
            PickSettlement.Settle(OddsMarketKeys.Ht1, noHalfTime));

        // İlk gol marketi skordan TÜRETİLEMEZ — İY skoru olsa bile.
        var withHalfTime = new PickSettlement.FinalScore(2, 1, 1, 0);
        Assert.False(PickSettlement.IsSupported(OddsMarketKeys.FirstGoalHome, withHalfTime));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Unsettleable,
            PickSettlement.Settle(OddsMarketKeys.FirstGoalHome, withHalfTime));

        // İY skoru VARSA ilk yarı marketi hesaplanabilir.
        Assert.True(PickSettlement.IsSupported(OddsMarketKeys.Ht1, withHalfTime));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.Ht1, withHalfTime));

        // Hesaplanamayan seçim "kaybetti" SAYILMAZ.
        Assert.Equal(Formax.Domain.Enums.PickStatus.Pending,
            PickSettlement.ToStatus(PickSettlement.PickSettlementOutcome.Unsettleable));
    }

    [Fact]
    public void Settlement_25CizgisiDogruSayilir()
    {
        // 2,5 ALT → toplam 2 ve azı. 2,5 ÜST → 3 ve fazlası. Tam 2 gol ALT'tır.
        var iki = new PickSettlement.FinalScore(1, 1, null, null);
        var uc = new PickSettlement.FinalScore(2, 1, null, null);

        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.Under25, iki));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Lost,
            PickSettlement.Settle(OddsMarketKeys.Over25, iki));
        Assert.Equal(PickSettlement.PickSettlementOutcome.Won,
            PickSettlement.Settle(OddsMarketKeys.Over25, uc));
    }

    // ── 41. KULLANICININ SEÇİMİ MODEL TAHMİNİ GİBİ ETİKETLENMEZ ─────────────────
    /// <summary>
    /// Kaydedilen yüzde SEÇİM ANININ anlık görüntüsüdür ve modele bağlı DEĞİLDİR:
    /// model sonradan güncellense bile kullanıcının gördüğü sayı değişmez. Bu,
    /// seçimin "modelin tahmini" olmadığının veri düzeyindeki karşılığıdır.
    /// </summary>
    [Fact]
    public async Task Secim_ModelinTahminiDegildir_AnlikGoruntuSaklanir()
    {
        var (useCase, repo) = Build();

        await useCase.ToggleAsync(User, new UserPickRequest
        {
            MatchId = MatchId,
            MarketLabel = "2.5 Alt",
            ProbabilityPercent = 61,
            Odd = 1.85m,
            ModelVersions = "INDEPENDENT_POISSON_V2/TEAM_STRENGTH_V2",
            ModelFingerprint = "fp-abc123"
        }, BeforeKickoff);

        var row = Assert.Single(repo.Rows);
        Assert.Equal(61, row.ProbabilityPercent);
        Assert.Equal(1.85m, row.OddAtSelection);
        Assert.Equal("INDEPENDENT_POISSON_V2/TEAM_STRENGTH_V2", row.ModelVersions);
        Assert.Equal("fp-abc123", row.ModelFingerprint);
        // Kaydın sahibi KULLANICIDIR; motorun tahmin tablosuna hiçbir şey yazılmaz.
        Assert.Equal(User, row.UserId);
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────────

    private static UserPickRequest Req(string label, int probability) => new()
    {
        MatchId = MatchId,
        MarketLabel = label,
        ProbabilityPercent = probability
    };

    private static (UserPickSelectionUseCase, FakePickRepo) Build(
        string status = MatchStatuses.NotStarted)
    {
        var repo = new FakePickRepo();
        var matches = new FakeMatchRepo(new Match
        {
            Id = MatchId,
            MatchDate = Kickoff,
            Status = status,
            LeagueId = 203,
            HomeTeamId = 1,
            AwayTeamId = 2
        });
        return (new UserPickSelectionUseCase(repo, matches), repo);
    }

    /// <summary>Bellek içi seçim deposu — gerçek DB yok.</summary>
    private sealed class FakePickRepo : IUserPickRepository
    {
        public List<UserPick> Rows { get; } = new();

        public Task<List<UserPick>> GetByMatchId(int matchId)
            => Task.FromResult(Rows.Where(r => r.MatchId == matchId).ToList());

        public Task Update(UserPick pick) => Task.CompletedTask;

        public Task Add(UserPick pick) { Rows.Add(pick); return Task.CompletedTask; }

        public Task SaveChanges() => Task.CompletedTask;

        public Task<List<UserPick>> GetByUserAndMatchAsync(
            string userId, int matchId, CancellationToken ct = default)
            => Task.FromResult(Rows.Where(r => r.UserId == userId && r.MatchId == matchId).ToList());

        public Task<List<UserPick>> GetByUserAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(Rows.Where(r => r.UserId == userId).ToList());

        public Task RemoveAsync(Guid id, CancellationToken ct = default)
        {
            Rows.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }

        public Task RemoveRangeAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var set = ids.ToHashSet();
            Rows.RemoveAll(r => set.Contains(r.Id));
            return Task.CompletedTask;
        }
    }
}
