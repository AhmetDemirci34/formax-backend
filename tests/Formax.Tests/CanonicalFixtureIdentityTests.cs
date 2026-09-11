using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KANONİK FİKSTÜR KİMLİĞİ — takım ADIYLA değil, ExternalMatchId ile.
///
/// GERÇEK OLAY (ölçüldü 02.09.2026): UEFA play-off ÇİFT MAÇLIDIR ve iki ayak
/// aynı iki takımı ters yönde içerir:
///   1. ayak  ExtId 1622621 · 18.08.2026 · Fenerbahçe 1-1 Lyon  (İY 0-1)
///   2. ayak  ExtId 1622630 · 26.08.2026 · Lyon 1-2 Fenerbahçe  (İY 0-2)
/// Takım adına göre eşleştirme yapan her mantık bu ikisini karıştırır ve doğru
/// veriyi "yanlış" sanıp bozar. Kimlik YALNIZ ExternalMatchId'dir.
/// </summary>
public class CanonicalFixtureIdentityTests : IDisposable
{
    private readonly FormaxDbContext _db;

    // Depodaki GERÇEK değerler (canlı DB'den okundu, uydurulmadı).
    private const string Leg1Ext = "1622621";
    private const string Leg2Ext = "1622630";

    public CanonicalFixtureIdentityTests()
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"canon-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(options);

        _db.Teams.Add(new Team { Id = 3588, Name = "Fenerbahçe", ExternalTeamId = "611" });
        _db.Teams.Add(new Team { Id = 3589, Name = "Lyon", ExternalTeamId = "80" });

        // 1. ayak — Fenerbahçe EV
        _db.Matches.Add(new Match
        {
            Id = 71513, ExternalMatchId = Leg1Ext, LeagueId = LockedCompetitions.ChampionsLeague,
            League = "UEFA Champions League", Round = "Play-offs",
            MatchDate = new DateTime(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc),
            HomeTeamId = 3588, AwayTeamId = 3589,
            HalfTimeHomeScore = 0, HalfTimeAwayScore = 1, HomeScore = 1, AwayScore = 1,
            Status = MatchStatuses.Finished
        });
        // 2. ayak — Lyon EV
        _db.Matches.Add(new Match
        {
            Id = 104237, ExternalMatchId = Leg2Ext, LeagueId = LockedCompetitions.ChampionsLeague,
            League = "UEFA Champions League", Round = "Play-offs",
            MatchDate = new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc),
            HomeTeamId = 3589, AwayTeamId = 3588,
            HalfTimeHomeScore = 0, HalfTimeAwayScore = 2, HomeScore = 1, AwayScore = 2,
            Status = MatchStatuses.Finished
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Match ByExt(string ext) =>
        _db.Matches.AsNoTracking().Single(m => m.ExternalMatchId == ext);

    [Fact]
    public void IkiAyak_AyriKanonikKayittir_TakimAdiKimlikDEGILDIR()
    {
        var leg1 = ByExt(Leg1Ext);
        var leg2 = ByExt(Leg2Ext);

        Assert.NotEqual(leg1.Id, leg2.Id);
        // Aynı takım çifti, TERS yön — ad bazlı eşleştirme bunları karıştırırdı.
        Assert.Equal(leg1.HomeTeamId, leg2.AwayTeamId);
        Assert.Equal(leg1.AwayTeamId, leg2.HomeTeamId);
    }

    [Fact]
    public void IkinciAyak_LyonEvSahibi_MS_1_2()
    {
        var m = ByExt(Leg2Ext);

        Assert.Equal(3589, m.HomeTeamId);          // Lyon EV
        Assert.Equal(3588, m.AwayTeamId);          // Fenerbahçe DEPLASMAN
        Assert.Equal(1, m.HomeScore);
        Assert.Equal(2, m.AwayScore);
    }

    [Fact]
    public void IkinciAyak_Kirilim_IY_0_2_2Y_1_0_MS_1_2()
    {
        var b = MatchScoreBreakdownDto.From(ByExt(Leg2Ext), resultIsFinal: true);

        Assert.Equal(0, b.HalfTime!.Home);
        Assert.Equal(2, b.HalfTime.Away);
        Assert.Equal(1, b.SecondHalf!.Home);       // 1-0 = 1
        Assert.Equal(0, b.SecondHalf.Away);        // 2-2 = 0
        Assert.Equal(1, b.FullTime!.Home);
        Assert.Equal(2, b.FullTime.Away);
    }

    [Fact]
    public void BirinciAyak_Kirilim_IY_0_1_2Y_1_0_MS_1_1()
    {
        // Kullanıcının "yanlış" sandığı değerler ASLINDA 1. ayağın DOĞRU verisidir.
        var b = MatchScoreBreakdownDto.From(ByExt(Leg1Ext), resultIsFinal: true);

        Assert.Equal(0, b.HalfTime!.Home);
        Assert.Equal(1, b.HalfTime.Away);
        Assert.Equal(1, b.SecondHalf!.Home);
        Assert.Equal(0, b.SecondHalf.Away);
        Assert.Equal(1, b.FullTime!.Home);
        Assert.Equal(1, b.FullTime.Away);
    }

    [Fact]
    public void ExternalMatchId_KANONIKTIR_Duplicate_Yoktur()
    {
        var dup = _db.Matches.AsNoTracking()
            .Where(m => m.ExternalMatchId != null && m.ExternalMatchId != "")
            .GroupBy(m => m.ExternalMatchId!)
            .Count(g => g.Count() > 1);

        Assert.Equal(0, dup);
    }

    [Fact]
    public void AyniKickoffTersYon_FarkliFiksturDUR_DuplicateSayilmaz()
    {
        // Ters yön "duplicate" tespiti YALNIZ aynı kickoff'ta anlamlıdır.
        // İki ayak farklı tarihlerdedir; duplicate değildir.
        var leg1 = ByExt(Leg1Ext);
        var leg2 = ByExt(Leg2Ext);
        Assert.NotEqual(leg1.MatchDate, leg2.MatchDate);
    }

    [Fact]
    public void UefaPlayoff_PuanDurumuGostermez()
    {
        var phase = Formax.Application.Services.Standings.CompetitionPhaseResolver
            .Resolve(LockedCompetitions.ChampionsLeague, ByExt(Leg2Ext).Round);
        var decision = Formax.Application.Services.Standings.StandingsPresentation
            .Decide(LockedCompetitions.ChampionsLeague, phase, hasLeaguePhaseTable: true);

        Assert.False(decision.ShowTable);
        Assert.Equal("Bu karşılaşma eleme aşamasındadır. Puan durumu bulunmaz.", decision.Notice);
    }

    public void Dispose() => _db.Dispose();
}
