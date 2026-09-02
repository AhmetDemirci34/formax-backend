using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// MAÇ SONRASI ÖZET — İY/2Y/MS kırılımı ve önemli anlar.
/// GERÇEK SAĞLAYICI ÇAĞRISI YOK: bütün girdiler bellekte kurulur.
/// </summary>
public class PostMatchSummaryTests
{
    private static Match M(int? htH, int? htA, int ftH, int ftA) => new()
    {
        Id = 1, HalfTimeHomeScore = htH, HalfTimeAwayScore = htA,
        HomeScore = ftH, AwayScore = ftA, Status = MatchStatuses.Finished
    };

    // ── Skor kırılımı ─────────────────────────────────────────────────────────

    [Fact]
    public void IkinciYari_MSeksiIY()
    {
        var b = MatchScoreBreakdownDto.From(M(1, 0, 1, 1), resultIsFinal: true);

        Assert.Equal(1, b.HalfTime!.Home);
        Assert.Equal(0, b.HalfTime.Away);
        Assert.Equal(0, b.SecondHalf!.Home);      // 1-1 = 0
        Assert.Equal(1, b.SecondHalf.Away);       // 1-0 = 1
        Assert.Equal(1, b.FullTime!.Home);
        Assert.Equal(1, b.FullTime.Away);
    }

    [Fact]
    public void IYYoksa_00Uydurulmaz()
    {
        var b = MatchScoreBreakdownDto.From(M(null, null, 2, 1), resultIsFinal: true);

        Assert.Null(b.HalfTime);                  // "İY —"
        Assert.Null(b.SecondHalf);                // türetilemez → "2Y —"
        Assert.NotNull(b.FullTime);               // MS gerçek
        Assert.Equal(2, b.FullTime!.Home);
    }

    [Fact]
    public void NegatifIkinciYari_VeriHatasi_Gosterilmez()
    {
        // İY 3-0 ama MS 1-0 → imkânsız. 2Y üretilmez.
        var b = MatchScoreBreakdownDto.From(M(3, 0, 1, 0), resultIsFinal: true);

        Assert.NotNull(b.HalfTime);
        Assert.NotNull(b.FullTime);
        Assert.Null(b.SecondHalf);
    }

    [Fact]
    public void MacBitmediyse_MSveIkinciYari_Uretilmez()
    {
        // Oynanmamış maçta HomeScore/AwayScore 0'dır; bu bir SONUÇ DEĞİLDİR.
        var b = MatchScoreBreakdownDto.From(M(null, null, 0, 0), resultIsFinal: false);

        Assert.Null(b.FullTime);
        Assert.Null(b.SecondHalf);
    }

    [Fact]
    public void UzatmaVePenalti_90DakikaSkoruylaKaristirilmaz()
    {
        var b = MatchScoreBreakdownDto.From(M(0, 0, 1, 1), resultIsFinal: true);

        // 90 dakika sonucu kendi alanında; uzatma/penaltı için depoda kaynak YOK.
        Assert.Equal(1, b.FullTime!.Home);
        Assert.Equal(1, b.FullTime.Away);
        Assert.Null(b.ExtraTime);                 // uydurulmaz
        Assert.Null(b.Penalties);                 // uydurulmaz
    }

    [Fact]
    public void SifirSifirBitenMac_GercekSonucturVeGosterilir()
    {
        var b = MatchScoreBreakdownDto.From(M(0, 0, 0, 0), resultIsFinal: true);

        Assert.NotNull(b.FullTime);               // 0-0 GERÇEK bir sonuçtur
        Assert.Equal(0, b.SecondHalf!.Home);
        Assert.Equal(0, b.SecondHalf.Away);
    }

    // ── Önemli anlar ──────────────────────────────────────────────────────────

    private static MatchLiveEvent E(int minute, string type, string? team = "A", string? player = null, string? detail = null)
        => new() { Id = Guid.NewGuid(), MatchId = 1, Minute = minute, EventType = type, Team = team, Player = player, Detail = detail };

    [Fact]
    public void OlayZamanCizelgesi_Kronolojiktir()
    {
        var events = new List<MatchLiveEvent>
        {
            E(67, "Goal"), E(12, "Card"), E(45, "Goal"), E(90, "subst")
        };

        var dto = MatchEventDto.FromEvents(events);

        Assert.Equal(new[] { 12, 45, 67, 90 }, dto.Select(x => x.Minute).ToArray());
    }

    [Fact]
    public void AyniOlayIkiKezYazilmissa_TekSatirOlur()
    {
        // Canlı alım aynı olayı iki turda yazabilir; zaman çizelgesi çift göstermemeli.
        var events = new List<MatchLiveEvent>
        {
            E(23, "Goal", "A", "A. Wahlman", "Normal Goal"),
            E(23, "Goal", "A", "A. Wahlman", "Normal Goal")
        };

        var dto = MatchEventDto.FromEvents(events);

        Assert.Single(dto);
    }

    [Fact]
    public void OlayYoksa_DurustBosListe()
    {
        Assert.Empty(MatchEventDto.FromEvents(null));
        Assert.Empty(MatchEventDto.FromEvents(new List<MatchLiveEvent>()));
    }

    [Fact]
    public void EksikAlanlar_UydurulmazNullKalir()
    {
        var dto = MatchEventDto.FromEvents(new List<MatchLiveEvent> { E(31, "Goal", team: "A") });
        var e = Assert.Single(dto);

        Assert.Equal(31, e.Minute);
        Assert.Equal("A", e.Team);
        Assert.Null(e.Player);      // kaynakta yok → uydurulmaz
        Assert.Null(e.Assist);
        Assert.Null(e.Detail);
    }
}
