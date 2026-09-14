using System;
using System.IO;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Services.PostMatch;
using Formax.Application.Services.Sapma;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// BİTMİŞ MAÇ: oyuncu değişikliği yönü, resmî video kimliği, arama durumu, maç sonrası analiz metni
/// ve ilk açılış SQL timeout regresyonu. GERÇEK İNTERNET YOK: bütün girdiler bellekte kurulur.
/// Olay ve başlıklar 12–13.09.2026'daki gerçek resmî kayıtlardan alınmıştır.
/// </summary>
public class FinishedMatchSummaryAndVideoTests
{
    // ── 1. GİREN / ÇIKAN ─────────────────────────────────────────────────────

    [Fact]
    public void OyuncuDegisikligi_PlayerCikan_AssistGiren_DtoyaDogruTasinir()
    {
        var dto = MatchEventDto.FromRecords(new[]
        {
            new MatchEventRecord { Minute = 44, EventType = "subst", Detail = "Substitution", TeamName = "Venezia",
                PlayerName = "Gianluca Busio", AssistName = "Þórir Helgason" }
        }).Single();

        Assert.Equal("Gianluca Busio", dto.PlayerOut);
        Assert.Equal("Þórir Helgason", dto.PlayerIn);
        Assert.Null(dto.Assist);
    }

    [Fact]
    public void SerieA_ResmiKadro_DegisiklikYonu_CikanPlayerGirenAssist()
    {
        // Resmî yükte giren oyuncu "substitution-in", çıkan "substitution-out" olayını taşır (15383, 44').
        const string json = """
        {"home":{"fielded":[{"playerId":"p-out","shortName":"Gianluca Busio","events":[{"type":"substitution-out","time":44,"additionalTime":0}]}],
                 "benched":[{"playerId":"p-in","shortName":"Þórir Helgason","events":[{"type":"substitution-in","time":44,"additionalTime":0}]}]},
         "away":{"fielded":[],"benched":[]}}
        """;
        var sub = SerieASdpSource.ParseEvents(json).Single(e => e.EventType == "subst");

        Assert.Equal("Gianluca Busio", sub.PlayerName);
        Assert.Equal("Þórir Helgason", sub.AssistName);
    }

    // ── 2. DÜN / BUGÜN BİTEN MAÇ VİDEO KEŞFİNE ADAYDIR ────────────────────────

    [Theory]
    [InlineData(-3)]    // bugün, 3 saat önce başlamış
    [InlineData(-26)]   // dün
    public void DunVeBugunBitenMac_IlkBakisaAdaydir(int kickoffHoursAgo)
    {
        var now = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var end = MatchVideoIdentityValidator.EndOf(now.AddHours(kickoffHoursAgo));
        Assert.True(PostMatchVideoSchedule.IsDue(end, 0, null, now));
    }

    [Fact]
    public void KanalSecimi_YalnizMacinLigiVeKulupleri()
    {
        var keys = OfficialVideoSources.DiscoverableYouTubeChannels("Aston Villa", "Nottingham Forest", 39)
            .Select(s => s.Key).ToList();

        Assert.Contains("aston-villa", keys);
        Assert.Contains("nottingham-forest", keys);
        Assert.Contains("premier-league-youtube", keys);
        Assert.DoesNotContain("rc-celta", keys);
        Assert.DoesNotContain("laliga-youtube", keys);
        Assert.DoesNotContain("fenerbahce", keys);
        Assert.DoesNotContain("bein-sports-turkiye", keys);
    }

    // ── 3. SAYFA AÇILIŞI KEŞİF / ÜRETİM / LLM BAŞLATMAZ ───────────────────────

    [Fact]
    public void MacDetayi_KesifUretimVeLlmCagirmaz()
    {
        var src = ReadRepo("Formax.Application/UseCases/GetMatchDetailAIContextUseCase.cs");

        Assert.DoesNotContain("DiscoverAsync", src);
        Assert.DoesNotContain("RegisterAsync", src);
        Assert.DoesNotContain("PostMatchSummaryComposer", src);
        Assert.DoesNotContain("PostMatchSummaryService", src);
        Assert.DoesNotContain("ILlm", src);
        Assert.Contains("_postMatchData.GetSummary(", src);
    }

    // ── 4. YANLIŞ MAÇ / TARİH / YÖN / SKOR REDDEDİLİR ─────────────────────────

    private const string ForestChannel = "UCyAxjuAr8f_BFDGCO3Htbxw";
    private static readonly DateTime VillaKickoff = new(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc);

    private static VideoFixtureIdentity VillaForest() => new(
        20370, "1557397", VillaKickoff, 2109, 2480, "Aston Villa", "Nottingham Forest",
        Array.Empty<DateTime>(), 39, 1, 2);

    private static OfficialVideoCandidate Candidate(string title, DateTime published, string channel = ForestChannel)
        => new("YouTube", channel, "vid-" + Math.Abs(title.GetHashCode()), title, null, published,
            "https://www.youtube.com/watch?v=x", null, null);

    [Fact]
    public void GercekResmiOzet_DogruMacaBaglanir()
    {
        var v = MatchVideoIdentityValidator.Validate(
            Candidate("IGOR JESUS LATE WINNER! 🇧🇷 | Aston Villa 1-2 Nottingham Forest | Premier League Highlights 🎬",
                new DateTime(2026, 9, 12, 21, 0, 28, DateTimeKind.Utc)),
            VillaForest());

        Assert.True(v.Accepted, v.Reason);
        Assert.Equal(MatchVideoTypes.MatchHighlights, v.VideoType);
    }

    [Fact]
    public void BaslikSkoruKayitliSonuclaUyusmuyorsa_Reddedilir()
    {
        var v = MatchVideoIdentityValidator.Validate(
            Candidate("Aston Villa 2-1 Nottingham Forest | Premier League Highlights",
                new DateTime(2026, 9, 12, 21, 0, 0, DateTimeKind.Utc)),
            VillaForest());

        Assert.False(v.Accepted);
        Assert.Contains("skor", v.Reason);
    }

    [Fact]
    public void TersYon_Reddedilir()
    {
        var v = MatchVideoIdentityValidator.Validate(
            Candidate("Nottingham Forest 2-1 Aston Villa | Premier League Highlights",
                new DateTime(2026, 9, 12, 21, 0, 0, DateTimeKind.Utc)),
            VillaForest());

        Assert.False(v.Accepted);
        Assert.Contains("ters", v.Reason);
    }

    [Fact]
    public void MactanOnceYayimlanmis_ya_da_PencereDisi_Reddedilir()
    {
        const string title = "Aston Villa 1-2 Nottingham Forest | Premier League Highlights";
        Assert.False(MatchVideoIdentityValidator.Validate(
            Candidate(title, VillaKickoff.AddMinutes(30)), VillaForest()).Accepted);
        Assert.False(MatchVideoIdentityValidator.Validate(
            Candidate(title, VillaKickoff.AddDays(20)), VillaForest()).Accepted);
    }

    [Fact]
    public void KulupKanali_KanonikKulupAdiyla_EvSahibiniDogrular()
    {
        var fixture = new VideoFixtureIdentity(15432, "1570374", new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc),
            1904, 1909, "Celta Vigo", "Malaga", Array.Empty<DateTime>(), 140, 1, 1);

        var celta = MatchVideoIdentityValidator.Validate(
            Candidate("Celta vs Málaga (1-1) | Resumen y highlights | Celta",
                new DateTime(2026, 9, 13, 21, 0, 13, DateTimeKind.Utc), "UCCJLVZYqRb_85b2Flpg04cg"), fixture);
        var malaga = MatchVideoIdentityValidator.Validate(
            Candidate("RESUMEN J5 | RC Celta 1-1 Málaga CF | Highlights LaLiga EA Sports",
                new DateTime(2026, 9, 13, 15, 29, 7, DateTimeKind.Utc), "UCo_PhWZulZooYfQRo00vU-Q"), fixture);
        var wrongScore = MatchVideoIdentityValidator.Validate(
            Candidate("Celta vs Málaga (2-0) | Resumen y highlights | Celta",
                new DateTime(2026, 9, 13, 21, 0, 13, DateTimeKind.Utc), "UCCJLVZYqRb_85b2Flpg04cg"), fixture);

        Assert.True(celta.Accepted, celta.Reason);
        Assert.True(malaga.Accepted, malaga.Reason);
        Assert.False(wrongScore.Accepted);
    }

    [Fact]
    public void SkorOkuma_RegexHataVermez_VeYonuCevirir()
    {
        var folded = MatchVideoIdentityValidator.Fold("Lobotka Edges Bologna | NAPOLI-BOLOGNA | HIGHLIGHTS");
        var none = MatchVideoIdentityValidator.ReadScore(folded, "Napoli", "Bologna");
        Assert.Equal(MatchVideoIdentityValidator.Direction.NotAsserted, none.Direction);

        var rev = MatchVideoIdentityValidator.ReadScore(
            MatchVideoIdentityValidator.Fold("Nottingham Forest 2-1 Aston Villa"), "Aston Villa", "Nottingham Forest");
        Assert.Equal((MatchVideoIdentityValidator.Direction.Reversed, (int?)1, (int?)2), rev);
    }

    // ── 5. GOL KLİBİ TAM ÖZET SAYILMAZ ────────────────────────────────────────

    [Fact]
    public void GolKlibi_TamOzetOlarakSiniflandirilmaz()
    {
        Assert.Equal(MatchVideoTypes.Goal,
            MatchVideoIdentityValidator.ClassifyType(MatchVideoIdentityValidator.Fold("Igor Jesus late goal vs Aston Villa")));
        Assert.Equal(MatchVideoTypes.MatchHighlights,
            MatchVideoIdentityValidator.ClassifyType(MatchVideoIdentityValidator.Fold("Aston Villa 1-2 Forest | Highlights & goals")));
    }

    // ── 6. VİDEO DURUMU DEFTERE BAĞLIDIR; 24 SAAT SESSİZLİK "KONTROL EDİLİYOR" DEĞİLDİR ──

    [Fact]
    public void AramaDurumu_DefterdenVeSessizliktenCozulur()
    {
        var end = MatchVideoIdentityValidator.EndOf(VillaKickoff);
        var oneAttempt = new FixtureAttemptSummary(1, new DateTime(2026, 9, 13, 16, 9, 21, DateTimeKind.Utc), "NoData", false);

        var fresh = PostMatchVideoSearchStatus.ResolveWithReason(false, oneAttempt, end, oneAttempt.LastAttemptUtc!.Value.AddHours(3));
        var stale = PostMatchVideoSearchStatus.ResolveWithReason(false, oneAttempt, end, oneAttempt.LastAttemptUtc!.Value.AddHours(25));
        var found = PostMatchVideoSearchStatus.ResolveWithReason(true, oneAttempt, end, oneAttempt.LastAttemptUtc!.Value.AddHours(25));
        var neverTried = PostMatchVideoSearchStatus.ResolveWithReason(false, FixtureAttemptSummary.None, end, end.AddHours(26));

        Assert.Equal((PostMatchVideoSearchStatus.Checking, (string?)null), fresh);
        Assert.Equal((PostMatchVideoSearchStatus.NotFound, PostMatchVideoSearchStatus.ReasonNoAttemptFor24h), stale);
        Assert.Equal(PostMatchVideoSearchStatus.Found, found.Status);
        Assert.Equal(PostMatchVideoSearchStatus.NotFound, neverTried.Status);
    }

    // ── 7. MAÇ SONRASI METİN YALNIZ DOĞRULANMIŞ VERİYİ KULLANIR ──────────────

    private static MatchEventRecord Ev(int min, string type, string detail, string team, string player, int? extra = null)
        => new() { Minute = min, ExtraMinute = extra, EventType = type, Detail = detail, TeamName = team, PlayerName = player };

    private static PostMatchSummaryResult VillaSummary()
    {
        var records = new[]
        {
            Ev(43, "Card", "Yellow Card", "Nottingham Forest", "Dan Ndoye"),
            Ev(46, "Goal", "Normal Goal", "Nottingham Forest", "Liam Delap"),
            Ev(74, "Goal", "Normal Goal", "Aston Villa", "Alysson"),
            Ev(88, "Goal", "Normal Goal", "Nottingham Forest", "Igor Jesus"),
        };
        return PostMatchSummaryComposer.Compose(new PostMatchSummaryInput(
            "Aston Villa", "Nottingham Forest", 1, 2, 0, 0,
            PostMatchSummaryComposer.FromRecords(records, "Aston Villa", "Nottingham Forest"),
            new PostMatchFactStats(54, 46, 5, 7)));
    }

    [Fact]
    public void Metin_SkorDevreGolVeIstatistiktenKurulur_UydurmaYok()
    {
        var s = VillaSummary();

        Assert.InRange(s.Sentences.Count, 2, 4);
        Assert.Equal("Nottingham Forest, Aston Villa deplasmanında 1-2 kazandı; ilk yarı 0-0 tamamlanmıştı.", s.Sentences[0]);
        Assert.Equal("Goller: 46' Liam Delap (Nottingham Forest), 74' Alysson (Aston Villa), 88' Igor Jesus (Nottingham Forest).", s.Sentences[1]);
        Assert.Contains("88' Igor Jesus", s.Sentences[2]);
        Assert.Contains("isabetli şut 5-7", s.Text);
        // Veride olmayan oyuncu, sebep-sonuç ya da taktik ifadesi yok.
        foreach (var banned in new[] { "hak etti", "baskı", "taktik", "savunma", "form", "Watkins" })
            Assert.DoesNotContain(banned, s.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OlaylarSkorlaCelisiyorsa_GolListesiYazilmaz()
    {
        var s = PostMatchSummaryComposer.Compose(new PostMatchSummaryInput(
            "Celta Vigo", "Malaga", 1, 1, 1, 0,
            PostMatchSummaryComposer.FromRecords(new[] { Ev(41, "Goal", "Normal Goal", "Celta Vigo", "Ferran Jutglà") },
                "Celta Vigo", "Malaga"),
            null));

        Assert.Single(s.Sentences);
        Assert.Equal("Celta Vigo ile Malaga 1-1 berabere kaldı; ilk yarı 1-0 tamamlanmıştı.", s.Sentences[0]);
    }

    [Fact]
    public void FarkliMaclar_AyniMetniAlmaz_VeAyniGirdiAyniMetniUretir()
    {
        var villa = VillaSummary();
        var brighton = PostMatchSummaryComposer.Compose(new PostMatchSummaryInput(
            "Coventry", "Brighton", 0, 2, 0, 1,
            PostMatchSummaryComposer.FromRecords(new[]
            {
                Ev(35, "Goal", "Normal Goal", "Brighton", "Charalampos Kostoulas"),
                Ev(70, "Goal", "Penalty", "Brighton", "Pascal Groß"),
            }, "Coventry", "Brighton"), null));

        Assert.NotEqual(villa.Text, brighton.Text);
        Assert.Contains("70' Pascal Groß (Brighton, penaltı)", brighton.Text);
        // Fark 2 ve galip geriye düşmedi: "belirleyici gol" cümlesi kurulmaz.
        Assert.DoesNotContain("belirleyen", brighton.Text);
        Assert.Equal(villa.InputHash, VillaSummary().InputHash);
        Assert.Equal(villa.Text, VillaSummary().Text);
    }

    [Fact]
    public void KaciranPenalti_GolSayilmaz_KendiKalesineGolde_AkisCumlesiKurulmaz()
    {
        var events = PostMatchSummaryComposer.FromRecords(new[]
        {
            Ev(10, "Goal", "Missed Penalty", "Lecce", "X"),
            Ev(20, "Goal", "Own Goal", "Monza", "Y"),
        }, "Lecce", "Monza");
        var s = PostMatchSummaryComposer.Compose(new PostMatchSummaryInput("Lecce", "Monza", 1, 0, 1, 0, events, null));

        Assert.Single(events);
        Assert.Contains("kendi kalesine", s.Text);
        Assert.DoesNotContain("belirleyen", s.Text);
    }

    [Fact]
    public void AyniOyuncununArdArdaIkiGolu_BirlestirilmezVeListeYazilir()
    {
        // 15383 Venezia 2-4 Fiorentina: Mastantuono 29' ve 30' iki ayrı gol attı.
        var records = new[]
        {
            Ev(22, "Goal", "Normal Goal", "Venezia", "Akor Adams"),
            Ev(29, "Goal", "Normal Goal", "Fiorentina", "Franco Mastantuono"),
            Ev(30, "Goal", "Normal Goal", "Fiorentina", "Franco Mastantuono"),
            Ev(66, "Goal", "Normal Goal", "Fiorentina", "Mateo Pellegrino"),
            Ev(84, "Goal", "Normal Goal", "Fiorentina", "Franco Mastantuono"),
            Ev(86, "Goal", "Normal Goal", "Venezia", "Antoine Hainaut"),
        };
        var s = PostMatchSummaryComposer.Compose(new PostMatchSummaryInput("Venezia", "Fiorentina", 2, 4, null, null,
            PostMatchSummaryComposer.FromRecords(records, "Venezia", "Fiorentina"), null));

        Assert.Contains("29' Franco Mastantuono (Fiorentina), 30' Franco Mastantuono (Fiorentina)", s.Text);
        Assert.Equal(6, s.Sentences[1].Split("' ").Length - 1);
    }

    // ── 8. İLK AÇILIŞ SQL TIMEOUT REGRESYONU ─────────────────────────────────

    [Fact]
    public void LigOrtalamasiSorgusu_YalnizSkorKolonlariniOkur_GenisSatirSiralamaz()
    {
        using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True").Options);

        var sql = GucSkoruCalculator.LeagueBaselineSampleQuery(
            db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam)).ToQueryString();

        Assert.Contains("[HomeScore]", sql);
        Assert.Contains("[AwayScore]", sql);
        Assert.Contains("TOP(", sql);
        // Ölçülen kök neden: nvarchar(max) kolonları + takım JOIN'i 252 MB bellek izni istiyordu.
        foreach (var wide in new[] { "[Venue]", "[Referee]", "[ResultSource]", "[MatchMinute]", "JOIN" })
            Assert.DoesNotContain(wide, sql);
    }

    private static string ReadRepo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
    }
}
