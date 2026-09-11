using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.PostMatch;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// BİTMİŞ MAÇ OLAY + İSTATİSTİK HATTI — aday kuralı, tekilleştirme ve boş cevap ayrımı.
///
/// Gerçek internet KULLANILMAZ: bütün girdiler bellekte kurulur.
/// </summary>
public class PostMatchDataPolicyTests
{
    private const int SuperLig = 203;
    private const int KapsamDisiLig = 9999;
    private const string Fixture = "1584377";   // Çorum FK – Kasımpaşa (gerçek fikstür kimliği)

    private static readonly DateTime Kickoff = new(2026, 8, 22, 16, 0, 0, DateTimeKind.Utc);

    /// <summary>Maç bitişi + 15 dk payı geçmiş bir an.</summary>
    private static DateTime AfterSettle =>
        PostMatchDataPolicy.EndOf(Kickoff) + PostMatchDataPolicy.SettleDelay + TimeSpan.FromMinutes(1);

    private static bool IsCandidate(
        string status = MatchStatuses.Finished,
        int leagueId = SuperLig,
        string? fixtureId = Fixture,
        bool alreadyHasData = false,
        bool scoreIsDefinite = true,
        DateTime? nowUtc = null)
        => PostMatchDataPolicy.IsCandidate(
            status, leagueId, fixtureId, alreadyHasData, scoreIsDefinite,
            Kickoff, nowUtc ?? AfterSettle);

    // ── 1. FINISHED MAÇ ADAY OLUR ───────────────────────────────────────────────
    [Fact]
    public void FinishedMac_AdayOlur() => Assert.True(IsCandidate());

    // ── 2. NotStarted / Live ADAY OLMAZ ─────────────────────────────────────────
    [Theory]
    [InlineData(MatchStatuses.NotStarted)]
    [InlineData(MatchStatuses.Live)]
    [InlineData(MatchStatuses.Postponed)]
    [InlineData(MatchStatuses.Cancelled)]
    public void BitmemisMac_AdayOlmaz(string status)
        => Assert.False(IsCandidate(status: status));

    // ── 3. KİLİTLİ LİG DIŞI ADAY OLMAZ ──────────────────────────────────────────
    [Fact]
    public void KilitliLigDisi_AdayOlmaz()
        => Assert.False(IsCandidate(leagueId: KapsamDisiLig));

    [Fact]
    public void KilitliLiglerin_HepsiKapsamda()
    {
        // Kilitli 11 organizasyonun HEPSİ aday üretebilmeli — kapsam daraltılmadı.
        foreach (var leagueId in LockedCompetitions.All)
            Assert.True(IsCandidate(leagueId: leagueId));
    }

    [Fact]
    public void SaglayiciKimligiYok_AdayOlmaz()
    {
        Assert.False(IsCandidate(fixtureId: null));
        Assert.False(IsCandidate(fixtureId: "   "));
    }

    [Fact]
    public void SkorKesinDegil_AdayOlmaz()
        => Assert.False(IsCandidate(scoreIsDefinite: false));

    [Fact]
    public void MacBitisindenOnce_AdayOlmaz()
    {
        // Maç henüz sürerken de, biter bitmez de aday DEĞİL: 15 dk pay beklenir.
        Assert.False(IsCandidate(nowUtc: Kickoff.AddMinutes(30)));
        Assert.False(IsCandidate(nowUtc: PostMatchDataPolicy.EndOf(Kickoff)));
        Assert.False(IsCandidate(nowUtc: PostMatchDataPolicy.EndOf(Kickoff).AddMinutes(14)));
        Assert.True(IsCandidate(nowUtc: PostMatchDataPolicy.EndOf(Kickoff).AddMinutes(15)));
    }

    // ── 6. VERİ ZATEN VARSA GERÇEK İSTEK ÜRETİLMEZ ──────────────────────────────
    [Fact]
    public void VeriZatenVarsa_AdayOlmaz()
        => Assert.False(IsCandidate(alreadyHasData: true));

    // ── 4/5. BÜTÇE SABİTLERİ ÜRÜN KARARINA UYAR ─────────────────────────────────
    [Fact]
    public void ButceSabitleri_UrunKararinaUyar()
    {
        Assert.Equal(5, PostMatchDataPolicy.DefaultEventsPerUtcDay);
        Assert.Equal(5, PostMatchDataPolicy.DefaultStatisticsPerUtcDay);
        Assert.Equal(TimeSpan.FromHours(24), PostMatchDataPolicy.PerFixtureCooldown);
        Assert.Equal(TimeSpan.FromMinutes(15), PostMatchDataPolicy.SettleDelay);
    }

    /// <summary>
    /// OLAY VE İSTATİSTİK AYRI AMAÇLARDIR — biri diğerinin bütçesini yiyemez.
    /// Aynı amaç adı kullanılsaydı 5 olay isteği, istatistik hakkını sıfırlardı.
    /// </summary>
    [Fact]
    public void OlayVeIstatistik_AyriButcelerdedir()
    {
        Assert.NotEqual(FixtureRefreshPurposes.PostMatchEvents,
                        FixtureRefreshPurposes.PostMatchStatistics);
        Assert.NotEqual(FixtureRefreshPurposes.PostMatchEvents,
                        FixtureRefreshPurposes.PostMatchVideo);
        Assert.NotEqual(FixtureRefreshPurposes.Lineup,
                        FixtureRefreshPurposes.PostMatchEvents);
    }

    // ── 8. DUPLICATE OLAY YAZILMAZ ──────────────────────────────────────────────
    [Fact]
    public void AyniOlay_AyniAnahtariUretir()
    {
        var a = Event(23, "Goal", "Normal Goal", "Oyuncu A");
        var b = Event(23, "Goal", "Normal Goal", "Oyuncu A");

        Assert.Equal(
            PostMatchEventKey.Build(Fixture, a),
            PostMatchEventKey.Build(Fixture, b));
    }

    [Fact]
    public void FarkliOlaylar_FarkliAnahtarUretir()
    {
        var gol = Event(23, "Goal", "Normal Goal", "Oyuncu A");
        var baskaDakika = Event(67, "Goal", "Normal Goal", "Oyuncu A");
        var baskaOyuncu = Event(23, "Goal", "Normal Goal", "Oyuncu B");
        var baskaTur = Event(23, "Card", "Yellow Card", "Oyuncu A");

        var keys = new[] { gol, baskaDakika, baskaOyuncu, baskaTur }
            .Select(e => PostMatchEventKey.Build(Fixture, e))
            .ToList();

        Assert.Equal(4, keys.Distinct().Count());
    }

    [Fact]
    public void UzatmaDakikasi_AnahtariAyristirir()
    {
        var normal = Event(90, "Goal", "Normal Goal", "Oyuncu A");
        var uzatma = Event(90, "Goal", "Normal Goal", "Oyuncu A");
        uzatma.ExtraMinute = 3;

        // 90. dakika golü ile 90+3 golü AYNI olay DEĞİLDİR.
        Assert.NotEqual(
            PostMatchEventKey.Build(Fixture, normal),
            PostMatchEventKey.Build(Fixture, uzatma));
    }

    [Fact]
    public void BuyukKucukHarfVeBosluk_AnahtariDegistirmez()
    {
        var a = Event(23, "Goal", "Normal Goal", "Oyuncu A");
        var b = Event(23, "goal", "normal goal", "  Oyuncu A  ");

        Assert.Equal(
            PostMatchEventKey.Build(Fixture, a),
            PostMatchEventKey.Build(Fixture, b));
    }

    [Fact]
    public void FarkliFikstur_AyniOlay_FarkliAnahtar()
    {
        var e = Event(23, "Goal", "Normal Goal", "Oyuncu A");
        Assert.NotEqual(
            PostMatchEventKey.Build("1584377", e),
            PostMatchEventKey.Build("1584397", e));
    }

    // ── 9. BOŞ İSTATİSTİK GERÇEK İSTATİSTİK SAYILMAZ ────────────────────────────
    [Fact]
    public void BosCevap_GercekIstatistikSayilmaz()
    {
        // Sağlayıcı cevap verdi ama HİÇBİR ölçüm yok: satır yazılmamalı.
        var empty = new SportsMatchStatisticsResult
        {
            Succeeded = true,
            Teams = new List<SportsTeamMatchStatistics>
            {
                new(), new()      // tüm alanlar null
            }
        };

        Assert.True(empty.Succeeded);
        Assert.False(empty.HasRealData);
        Assert.All(empty.Teams, t => Assert.False(t.HasAnyMeasurement));
    }

    [Fact]
    public void TekOlcumVarsa_GercekIstatistikSayilir()
    {
        var one = new SportsMatchStatisticsResult
        {
            Succeeded = true,
            Teams = new List<SportsTeamMatchStatistics>
            {
                new() { Corners = 0 },   // 0 KORNER GERÇEK BİR ÖLÇÜMDÜR
                new()
            }
        };

        Assert.True(one.HasRealData);
        Assert.True(one.Teams[0].HasAnyMeasurement);
        Assert.False(one.Teams[1].HasAnyMeasurement);
    }

    /// <summary>
    /// SAĞLAYICI HATASI ≠ "VERİ YOK". Hata durumunda hiçbir şey yazılmamalı ki
    /// maç yeniden denenebilsin; boş satır yazılsaydı "veri var" sayılır ve
    /// gerçek istatistik kalıcı olarak kaybolurdu.
    /// </summary>
    [Fact]
    public void SaglayiciHatasi_VeriYokDemekDegildir()
    {
        var failed = SportsMatchStatisticsResult.Failed();
        Assert.False(failed.Succeeded);
        Assert.False(failed.HasRealData);
        Assert.Empty(failed.Teams);

        var failedEvents = SportsMatchEventsResult.Failed();
        Assert.False(failedEvents.Succeeded);
        Assert.Empty(failedEvents.Events);
    }

    // ── 7. KISMİ BAŞARI KORUNUR ─────────────────────────────────────────────────
    /// <summary>
    /// Olaylar geldi, istatistik gelmedi: iki uç AYRI amaçlar olduğu için olaylar
    /// yazılır ve istatistik ertesi gün yeniden denenir. Tek bir "maç detayı"
    /// çağrısı olsaydı, istatistiğin hatası olayları da çöpe atardı.
    /// </summary>
    [Fact]
    public void KismiBasari_OlaylarKorunur()
    {
        var events = new SportsMatchEventsResult
        {
            Succeeded = true,
            Events = new List<SportsMatchEvent> { Event(23, "Goal", "Normal Goal", "Oyuncu A") }
        };
        var stats = SportsMatchStatisticsResult.Failed();

        Assert.True(events.Succeeded);
        Assert.Single(events.Events);
        Assert.False(stats.Succeeded);

        // Aday kuralı da bunu destekler: istatistik hâlâ eksik olduğu için maç
        // (olaylar yazılmış olsa bile) istatistik uğruna aday kalmaya devam eder.
        Assert.True(IsCandidate(alreadyHasData: false));
    }

    private static SportsMatchEvent Event(int minute, string type, string detail, string player)
        => new()
        {
            Minute = minute,
            EventType = type,
            Detail = detail,
            PlayerName = player,
            TeamExternalId = 611
        };
}
