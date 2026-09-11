using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// BİTMİŞ MAÇ OKUMA YOLU — kanonik kayıt ekrana taşınır, uydurma sıfır gösterilmez.
///
/// ÖLÇÜLEN GERÇEK (06.09.2026): eski okuma yolu <c>MatchLiveStats</c> tablosundan
/// besleniyordu ve o tablodaki 87.546 satırın 87.502'si skor dışında TAMAMEN sıfırdı.
/// Bütün alanlar <c>int</c> olduğu için "0 korner" ile "korner bilgisi yok" ayırt
/// edilemiyordu. Kanonik satırda her ölçüm nullable'dır: veri yoksa SATIR ÜRETİLMEZ.
/// </summary>
public class PostMatchReadPathTests
{
    private const int MatchId = 99157;   // Çorum FK – Eyüpspor (gerçek maç)

    private static MatchEventRecord Event(
        int minute, string type, string detail, string team, string? player, string? assist = null,
        int? extra = null) => new()
    {
        MatchId = MatchId,
        ExternalFixtureId = "1584395",
        ProviderEventId = $"{minute}-{type}-{player}",
        Minute = minute,
        ExtraMinute = extra,
        EventType = type,
        Detail = detail,
        TeamName = team,
        PlayerName = player,
        AssistName = assist,
        Source = "api-football",
        FetchedAtUtc = TestData.Now
    };

    // ── OLAYLAR ─────────────────────────────────────────────────────────────────

    [Fact]
    public void KanonikOlaylar_DakikaSirasiylaTasinir()
    {
        var records = new List<MatchEventRecord>
        {
            Event(67, "Card", "Yellow Card", "Eyüpspor", "Oyuncu C"),
            Event(23, "Goal", "Normal Goal", "Çorum FK", "Oyuncu A", assist: "Oyuncu B"),
            Event(90, "Goal", "Normal Goal", "Çorum FK", "Oyuncu D", extra: 3)
        };

        var dto = MatchEventDto.FromRecords(records);

        Assert.Equal(3, dto.Count);
        Assert.Equal(new[] { 23, 67, 90 }, dto.Select(e => e.Minute));

        var gol = dto[0];
        Assert.Equal("Goal", gol.EventType);
        Assert.Equal("Normal Goal", gol.Detail);
        Assert.Equal("Çorum FK", gol.Team);
        Assert.Equal("Oyuncu A", gol.Player);
        Assert.Equal("Oyuncu B", gol.Assist);      // ASİST TAŞINIR (eski yol taşımıyordu)
        Assert.Null(gol.ExtraMinute);

        // 90+3 → dakika ve uzatma AYRI kalır; tek sayıya ezilmez.
        Assert.Equal(90, dto[2].Minute);
        Assert.Equal(3, dto[2].ExtraMinute);
    }

    [Fact]
    public void AyniDakikadaIkiOlay_IkisiDeGorunur()
    {
        // Yazma yolunda tekilleştirme ProviderEventId ile yapılır; okuma yolu
        // FARKLI olayları birleştirmez — 33'te iki ayrı sarı kart iki satırdır.
        var records = new List<MatchEventRecord>
        {
            Event(33, "Card", "Yellow Card", "Çorum FK", "Oyuncu A"),
            Event(33, "Card", "Yellow Card", "Eyüpspor", "Oyuncu B")
        };

        Assert.Equal(2, MatchEventDto.FromRecords(records).Count);
    }

    [Fact]
    public void OlayYoksa_BosListe()
    {
        Assert.Empty(MatchEventDto.FromRecords(null));
        Assert.Empty(MatchEventDto.FromRecords(new List<MatchEventRecord>()));
    }

    // ── İSTATİSTİKLER ───────────────────────────────────────────────────────────

    private static MatchTeamStatistic Side(string side, Action<MatchTeamStatistic> fill)
    {
        var s = new MatchTeamStatistic
        {
            MatchId = MatchId, ExternalFixtureId = "1584395", Side = side,
            Source = "api-football", FetchedAtUtc = TestData.Now
        };
        fill(s);
        return s;
    }

    [Fact]
    public void KanonikIstatistik_GercekOlcumleriTasir()
    {
        // GERÇEK ölçülen değerler (99157, api-football): Çorum 60/19/9, Eyüpspor 40/8/4.
        var home = Side("Home", s => { s.BallPossession = 60; s.TotalShots = 19; s.ShotsOnTarget = 9; s.Corners = 8; });
        var away = Side("Away", s => { s.BallPossession = 40; s.TotalShots = 8;  s.ShotsOnTarget = 4; s.Corners = 3; });

        var dto = MatchStatisticsDto.FromTeamRows(home, away);

        Assert.NotNull(dto);
        var pos = dto!.Rows.Single(r => r.Key == "possession");
        Assert.Equal(60, pos.Home);
        Assert.Equal(40, pos.Away);
        Assert.True(pos.IsPercentage);
        Assert.Equal(19, dto.Rows.Single(r => r.Key == "shots").Home);
    }

    /// <summary>
    /// SAĞLAYICININ VERMEDİĞİ ÖLÇÜM SATIR ÜRETMEZ — 0 UYDURULMAZ.
    ///
    /// Ölçüldü: api-football bu ligde <c>"Passes %"</c> alanını göndermiyor. O alan null
    /// kaldığı için "Başarılı pas %" satırı ekranda HİÇ ÇIKMAZ; "%0 isabet" yazmak,
    /// olmayan veriyi var etmek olurdu.
    /// </summary>
    [Fact]
    public void VerilmeyenOlcum_SatirUretmez()
    {
        var home = Side("Home", s => { s.Corners = 8; s.PassAccuracy = null; });
        var away = Side("Away", s => { s.Corners = 3; s.PassAccuracy = null; });

        var dto = MatchStatisticsDto.FromTeamRows(home, away);

        Assert.NotNull(dto);
        Assert.Contains(dto!.Rows, r => r.Key == "corners");
        Assert.DoesNotContain(dto.Rows, r => r.Key == "passAccuracy");
    }

    [Fact]
    public void SifirGercekBirOlcumdur_SatirCikar()
    {
        // 0 korner GERÇEK bir ölçümdür ve gösterilir; null olan "bilgi yok"tur.
        var home = Side("Home", s => s.Corners = 0);
        var away = Side("Away", s => s.Corners = 0);

        var dto = MatchStatisticsDto.FromTeamRows(home, away);

        Assert.NotNull(dto);
        var row = dto!.Rows.Single(r => r.Key == "corners");
        Assert.Equal(0, row.Home);
        Assert.Equal(0, row.Away);
    }

    [Fact]
    public void TekTarafliOlcum_SatirUretmez()
    {
        // Karşılaştırma satırı tek taraflı kurulamaz.
        var home = Side("Home", s => s.Corners = 8);
        var away = Side("Away", s => s.Corners = null);

        Assert.Null(MatchStatisticsDto.FromTeamRows(home, away));
    }

    [Fact]
    public void HicOlcumYoksa_Null()
    {
        Assert.Null(MatchStatisticsDto.FromTeamRows(Side("Home", _ => { }), Side("Away", _ => { })));
        Assert.Null(MatchStatisticsDto.FromTeamRows(null, Side("Away", s => s.Corners = 3)));
        Assert.Null(MatchStatisticsDto.FromTeamRows(Side("Home", s => s.Corners = 3), null));
    }
}
