using System;
using System.Linq;
using System.Threading.Tasks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// SONUÇLAR SEKMESİ — gün bazlı bitmiş maç okuması.
///
/// GERÇEK API ÇAĞRISI YOK: depo EF InMemory'dir, hiçbir test internete ya da
/// api-football'a çıkmaz.
///
/// Veriler depodaki GERÇEK Fenerbahçe–Lyon play-off'undan alınmıştır:
///   MatchId 71513  · 18.08.2026 19:00 UTC (TR 22:00) · Fenerbahçe (ev) 1-1 Lyon
///   MatchId 104237 · 26.08.2026 19:00 UTC (TR 22:00) · Lyon (ev) 1-2 Fenerbahçe
/// </summary>
public class MatchResultsTests
{
    private const int Leg1Id = 71513, Leg2Id = 104237;
    private const int Fener = 3588, Lyon = 3589;
    private static readonly DateTime Leg1Kickoff = new(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Leg2Kickoff = new(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly Aug18 = new(2026, 8, 18);
    private static readonly DateOnly Aug26 = new(2026, 8, 26);

    private static FormaxDbContext NewDb(string name)
        => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Match M(
        int id, string externalId, DateTime kickoff, int leagueId, string status,
        int homeTeamId, int awayTeamId, int home, int away,
        int? htHome = null, int? htAway = null, string? round = null, string league = "UEFA Champions League")
        => new()
        {
            Id = id, ExternalMatchId = externalId, MatchDate = kickoff, LeagueId = leagueId,
            League = league, Round = round, Status = status,
            HomeTeamId = homeTeamId, AwayTeamId = awayTeamId,
            HomeScore = home, AwayScore = away,
            HalfTimeHomeScore = htHome, HalfTimeAwayScore = htAway
        };

    /// <summary>İki gerçek ayak + kurulum takımları.</summary>
    private static FormaxDbContext SeededDb(string name)
    {
        var db = NewDb(name);
        db.Teams.AddRange(
            new Team { Id = Fener, Name = "Fenerbahçe" },
            new Team { Id = Lyon, Name = "Lyon" });
        db.Matches.AddRange(
            M(Leg1Id, "1622621", Leg1Kickoff, LockedCompetitions.ChampionsLeague, MatchStatuses.Finished,
                Fener, Lyon, 1, 1, 0, 1, "Play-offs"),
            M(Leg2Id, "1622630", Leg2Kickoff, LockedCompetitions.ChampionsLeague, MatchStatuses.Finished,
                Lyon, Fener, 1, 2, 0, 2, "Play-offs"));
        db.SaveChanges();
        return db;
    }

    // ── 1-3. YALNIZ BİTMİŞ MAÇLAR ─────────────────────────────────────────────

    [Fact]
    public async Task YalnizBitmisMaclarDoner_NotStartedVeLiveGirmez()
    {
        using var db = SeededDb(nameof(YalnizBitmisMaclarDoner_NotStartedVeLiveGirmez));
        db.Matches.AddRange(
            // Aynı gün, aynı lig — ama oynanmamış.
            M(900001, "fx-ns", Leg1Kickoff.AddHours(-2), LockedCompetitions.ChampionsLeague,
                MatchStatuses.NotStarted, Fener, Lyon, 0, 0),
            // Aynı gün, aynı lig — hâlâ oynanıyor. Kickoff GEÇMİŞ olsa bile sonuç değildir.
            M(900002, "fx-live", Leg1Kickoff.AddMinutes(-30), LockedCompetitions.ChampionsLeague,
                MatchStatuses.Live, Fener, Lyon, 1, 0),
            // Ertelenen maç da sonuç değildir.
            M(900003, "fx-post", Leg1Kickoff.AddHours(-1), LockedCompetitions.ChampionsLeague,
                MatchStatuses.Postponed, Fener, Lyon, 0, 0));
        db.SaveChanges();

        var results = await TestReaders.Results(db).GetResultsAsync(Aug18);

        var only = Assert.Single(results);
        Assert.Equal(Leg1Id, only.MatchId);
        Assert.Equal(MatchStatuses.Finished, only.Status);
        Assert.DoesNotContain(results, r => r.MatchId is 900001 or 900002 or 900003);
    }

    // ── 4-5. KAPSAM SÜZGECİ ───────────────────────────────────────────────────

    [Fact]
    public async Task LeagueIdSifirKopyalari_SonucListesineGirmez()
    {
        using var db = SeededDb(nameof(LeagueIdSifirKopyalari_SonucListesineGirmez));
        // ÖLÇÜLDÜ (03.09.2026): depoda geçmiş ingestion'lardan kalan 21 bitmiş
        // LeagueId=0 satırı var. Bunlar kullanıcıya ASLA gösterilmez.
        db.Matches.Add(M(900010, "fx-zero", Leg1Kickoff, 0, MatchStatuses.Finished,
            Fener, Lyon, 1, 1));
        db.SaveChanges();

        var results = await TestReaders.Results(db).GetResultsAsync(Aug18);

        Assert.Single(results);
        Assert.DoesNotContain(results, r => r.LeagueId == 0);
    }

    [Fact]
    public async Task KilitliKapsamDisiLigler_Girmez()
    {
        using var db = SeededDb(nameof(KilitliKapsamDisiLigler_Girmez));
        // 94 = Primeira Liga, 71 = Brasileirão — ürün kararıyla KAPSAM DIŞI.
        db.Matches.AddRange(
            M(900020, "fx-94", Leg1Kickoff, 94, MatchStatuses.Finished, Fener, Lyon, 2, 0, league: "Primeira Liga"),
            M(900021, "fx-71", Leg1Kickoff, 71, MatchStatuses.Finished, Fener, Lyon, 3, 1, league: "Serie A"));
        db.SaveChanges();

        var results = await TestReaders.Results(db).GetResultsAsync(Aug18);

        Assert.Single(results);
        Assert.All(results, r => Assert.Contains(r.LeagueId, LockedCompetitions.All));
    }

    // ── 6. EUROPE/ISTANBUL GÜN SINIRI ─────────────────────────────────────────

    [Fact]
    public void IstanbulGunSiniri_UtcGunuDegildir()
    {
        // 26 Ağustos TÜRKİYE günü = 25.08 21:00 UTC → 26.08 21:00 UTC (UTC+3).
        var (start, end) = IstanbulCalendar.DayRangeUtc(Aug26);

        Assert.Equal(new DateTime(2026, 8, 25, 21, 0, 0, DateTimeKind.Utc), DateTime.SpecifyKind(start, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 8, 26, 21, 0, 0, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc));

        // 19:00 UTC kickoff Türkiye'de 22:00'dir ve 26 Ağustos'a AİTTİR.
        Assert.Equal(Aug26, IstanbulCalendar.TodayIn(Leg2Kickoff));
    }

    [Fact]
    public async Task GeceYarisinaYakinMac_DogruTurkiyeGunundeCikar()
    {
        using var db = SeededDb(nameof(GeceYarisinaYakinMac_DogruTurkiyeGunundeCikar));
        // 26.08 20:45 UTC = TR 27.08 23:45 → HAYIR: TR 26.08 23:45. Sınırın hemen altı.
        db.Matches.Add(M(900030, "fx-late", new DateTime(2026, 8, 26, 20, 45, 0, DateTimeKind.Utc),
            LockedCompetitions.SuperLig, MatchStatuses.Finished, Fener, Lyon, 2, 1, league: "Süper Lig"));
        // 26.08 21:15 UTC = TR 27.08 00:15 → ERTESİ Türkiye günü.
        db.Matches.Add(M(900031, "fx-next", new DateTime(2026, 8, 26, 21, 15, 0, DateTimeKind.Utc),
            LockedCompetitions.SuperLig, MatchStatuses.Finished, Fener, Lyon, 0, 3, league: "Süper Lig"));
        db.SaveChanges();

        var reader = TestReaders.Results(db);
        var aug26 = await reader.GetResultsAsync(Aug26);
        var aug27 = await reader.GetResultsAsync(new DateOnly(2026, 8, 27));

        Assert.Contains(aug26, r => r.MatchId == 900030);
        Assert.DoesNotContain(aug26, r => r.MatchId == 900031);
        Assert.Contains(aug27, r => r.MatchId == 900031);
    }

    // ── 7. TEKİLLEŞTİRME ──────────────────────────────────────────────────────

    [Fact]
    public async Task AyniExternalFixtureId_IkiKezDonmez()
    {
        using var db = SeededDb(nameof(AyniExternalFixtureId_IkiKezDonmez));
        // Aynı fikstür ikinci bir satıra düşmüş (farklı MatchId, aynı sağlayıcı kimliği).
        db.Matches.Add(M(900040, "1622621", Leg1Kickoff, LockedCompetitions.ChampionsLeague,
            MatchStatuses.Finished, Fener, Lyon, 1, 1, 0, 1, "Play-offs"));
        db.SaveChanges();

        var results = await TestReaders.Results(db).GetResultsAsync(Aug18);

        Assert.Single(results);
        // En küçük MatchId kazanır → kopya her çalıştırmada AYNI şekilde elenir.
        Assert.Equal(Leg1Id, results[0].MatchId);
    }

    // ── 8. DETERMİNİSTİK SIRALAMA ─────────────────────────────────────────────

    [Fact]
    public async Task Siralama_EnSonBitenOnce_EsitlikteMatchIdAzalan()
    {
        using var db = SeededDb(nameof(Siralama_EnSonBitenOnce_EsitlikteMatchIdAzalan));
        db.Matches.AddRange(
            M(900053, "fx-c", new DateTime(2026, 8, 18, 16, 0, 0, DateTimeKind.Utc),
                LockedCompetitions.SuperLig, MatchStatuses.Finished, Fener, Lyon, 1, 0, league: "Süper Lig"),
            M(900051, "fx-a", new DateTime(2026, 8, 18, 14, 0, 0, DateTimeKind.Utc),
                LockedCompetitions.PremierLeague, MatchStatuses.Finished, Fener, Lyon, 2, 2, league: "Premier League"),
            // Aynı dakikada başlayan iki maç — sıra MatchId azalan ile kesinleşir.
            M(900052, "fx-b", new DateTime(2026, 8, 18, 14, 0, 0, DateTimeKind.Utc),
                LockedCompetitions.LaLiga, MatchStatuses.Finished, Fener, Lyon, 0, 0, league: "La Liga"));
        db.SaveChanges();

        var reader = TestReaders.Results(db);
        var first = await reader.GetResultsAsync(Aug18);
        var second = await reader.GetResultsAsync(Aug18);

        Assert.Equal(new[] { Leg1Id, 900053, 900052, 900051 }, first.Select(r => r.MatchId).ToArray());
        Assert.Equal(first.Select(r => r.MatchId), second.Select(r => r.MatchId));
    }

    // ── 9. BUGÜN SONUÇ YOKSA EN YAKIN SONUÇLU GÜN ────────────────────────────

    [Fact]
    public async Task SonucluGunler_YenidenEskiyeDoner_EnYakinBasta()
    {
        var dbName = nameof(SonucluGunler_YenidenEskiyeDoner_EnYakinBasta);
        using var db = NewDb(dbName);
        db.Teams.AddRange(
            new Team { Id = Fener, Name = "Fenerbahçe" },
            new Team { Id = Lyon, Name = "Lyon" });

        // Bugün ve dün BOŞ; iki gün önce sonuç var. Seçici en yakın sonuçlu güne düşmeli.
        var today = IstanbulCalendar.TodayIn(DateTime.UtcNow);
        var twoDaysAgo = today.AddDays(-2);
        var fourDaysAgo = today.AddDays(-4);
        var (twoStart, _) = IstanbulCalendar.DayRangeUtc(twoDaysAgo);
        var (fourStart, _) = IstanbulCalendar.DayRangeUtc(fourDaysAgo);

        db.Matches.AddRange(
            M(900060, "fx-d2", twoStart.AddHours(19), LockedCompetitions.SuperLig,
                MatchStatuses.Finished, Fener, Lyon, 1, 0, league: "Süper Lig"),
            M(900061, "fx-d4", fourStart.AddHours(19), LockedCompetitions.SuperLig,
                MatchStatuses.Finished, Fener, Lyon, 2, 2, league: "Süper Lig"));
        db.SaveChanges();

        var days = await TestReaders.Results(db).GetRecentResultDaysAsync(8);

        Assert.DoesNotContain(days, d => d.Date == today.ToString("yyyy-MM-dd"));
        // İlk kayıt = EN YAKIN sonuçlu gün. Tarih seçici doğrudan bunu seçer.
        Assert.Equal(twoDaysAgo.ToString("yyyy-MM-dd"), days[0].Date);
        Assert.Equal(fourDaysAgo.ToString("yyyy-MM-dd"), days[1].Date);
        Assert.All(days, d => Assert.True(d.MatchCount > 0));
    }

    [Fact]
    public async Task SonucluGunler_KapsamDisiVeBitmemisMaclariSaymaz()
    {
        var dbName = nameof(SonucluGunler_KapsamDisiVeBitmemisMaclariSaymaz);
        using var db = NewDb(dbName);
        db.Teams.Add(new Team { Id = Fener, Name = "Fenerbahçe" });

        var today = IstanbulCalendar.TodayIn(DateTime.UtcNow);
        var (todayStart, _) = IstanbulCalendar.DayRangeUtc(today);

        db.Matches.AddRange(
            M(900070, "fx-ns2", todayStart.AddHours(12), LockedCompetitions.SuperLig,
                MatchStatuses.NotStarted, Fener, Lyon, 0, 0, league: "Süper Lig"),
            M(900071, "fx-zero2", todayStart.AddHours(13), 0,
                MatchStatuses.Finished, Fener, Lyon, 1, 1));
        db.SaveChanges();

        var days = await TestReaders.Results(db).GetRecentResultDaysAsync(8);

        // Bugün "sonuçlu gün" DEĞİLDİR: oynanmamış maç ve kapsam dışı satır sayılmaz.
        Assert.Empty(days);
    }

    // ── 11. İKİ AYAK BİRBİRİNE KARIŞMAZ ──────────────────────────────────────

    [Fact]
    public async Task IkiAyak_KendiGununde_KendiVerisiyleDoner()
    {
        using var db = SeededDb(nameof(IkiAyak_KendiGununde_KendiVerisiyleDoner));
        var reader = TestReaders.Results(db);

        var aug18 = Assert.Single(await reader.GetResultsAsync(Aug18));
        Assert.Equal(Leg1Id, aug18.MatchId);
        Assert.Equal("1622621", aug18.ExternalFixtureId);
        Assert.Equal("Fenerbahçe", aug18.HomeTeam.Name);   // 1. ayakta ev sahibi Fenerbahçe
        Assert.Equal("Lyon", aug18.AwayTeam.Name);
        Assert.Equal(1, aug18.HomeScore);
        Assert.Equal(1, aug18.AwayScore);
        Assert.Equal(0, aug18.HalfTimeHomeScore);
        Assert.Equal(1, aug18.HalfTimeAwayScore);

        var aug26 = Assert.Single(await reader.GetResultsAsync(Aug26));
        Assert.Equal(Leg2Id, aug26.MatchId);
        Assert.Equal("1622630", aug26.ExternalFixtureId);
        Assert.Equal("Lyon", aug26.HomeTeam.Name);          // 2. ayakta ev sahibi Lyon — TERS DEĞİL
        Assert.Equal("Fenerbahçe", aug26.AwayTeam.Name);
        Assert.Equal(1, aug26.HomeScore);
        Assert.Equal(2, aug26.AwayScore);
        Assert.Equal(0, aug26.HalfTimeHomeScore);
        Assert.Equal(2, aug26.HalfTimeAwayScore);

        // Bir ayağın maçı diğerinin gününde GÖRÜNMEZ.
        Assert.DoesNotContain(await reader.GetResultsAsync(Aug18), r => r.MatchId == Leg2Id);
        Assert.DoesNotContain(await reader.GetResultsAsync(Aug26), r => r.MatchId == Leg1Id);
    }

    // ── Video işareti: yalnız gerçekten oynatılabilir kayıt varsa ─────────────

    [Fact]
    public async Task VideoOzelligiKapali_OynatilabilirKayitOlsaBileSonucKartiVideoTasimaz()
    {
        using var db = SeededDb(nameof(VideoOzelligiKapali_OynatilabilirKayitOlsaBileSonucKartiVideoTasimaz));
        db.MatchVideos.AddRange(
            new MatchVideo
            {
                MatchId = Leg1Id, ExternalFixtureId = "1622621", ExternalVideoId = "v1",
                Title = "Özet", OfficialPublisher = "TRT SPOR",
                SourcePageUrl = "https://www.youtube.com/watch?v=v1",
                // Gömme adresi ZORUNLU: adresi olmayan kayıt oynatılabilir sayılmaz.
                EmbedUrl = "https://www.youtube-nocookie.com/embed/v1",
                VideoType = MatchVideoTypes.MatchHighlights,
                IsOfficial = true, IsEmbeddable = true, CanPlayInApp = true,
                DiscoveryProvenance = MatchVideoRules.OfficialWebProvenance, EvidencePageUrl = "https://www.trtspor.com.tr/video/test",
                VerificationStatus = MatchVideoVerificationStatuses.Verified
            },
            // 2. ayakta kayıt VAR ama oynatılamıyor → işaret GÖSTERİLMEZ.
            new MatchVideo
            {
                MatchId = Leg2Id, ExternalFixtureId = "1622630", ExternalVideoId = "v2",
                Title = "UEFA highlights", OfficialPublisher = "UEFA",
                SourcePageUrl = "https://www.uefa.com/x",
                VideoType = MatchVideoTypes.MatchHighlights,
                IsOfficial = true, IsEmbeddable = false, CanPlayInApp = false,
                VerificationStatus = MatchVideoVerificationStatuses.EmbedBlocked
            });
        db.SaveChanges();

        var reader = TestReaders.Results(db);
        // 15.09.2026: video özelliği kaldırıldı — DTO'da video alanı yok; eski kayıtlar sonuç kartına sızmaz.
        Assert.Null(typeof(Formax.Application.DTOs.Matches.MatchResultItemDto).GetProperty("HasPlayableOfficialVideo"));
        var json = System.Text.Json.JsonSerializer.Serialize(await reader.GetResultsAsync(Aug18));
        Assert.DoesNotContain("Video", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BosGun_HataDegilBosListedir()
    {
        using var db = SeededDb(nameof(BosGun_HataDegilBosListedir));
        var results = await TestReaders.Results(db).GetResultsAsync(new DateOnly(2026, 8, 20));
        Assert.Empty(results);
    }
}
