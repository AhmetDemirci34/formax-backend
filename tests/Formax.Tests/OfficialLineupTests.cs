using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Notifications;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// RESMÎ KADRO — ayrıştırıcılar GERÇEK resmî cevaplarla (11.09.2026'da kaydedilmiş, kırpılmış),
/// toplayıcı bellekte DB ve sahte kaynakla sınanır. API-Football yok, gerçek ağ yok.
/// </summary>
public class OfficialLineupTests
{
    internal static string Fixture(string name)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir!, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private static readonly DateTime Kickoff = new(2026, 9, 11, 18, 45, 0, DateTimeKind.Utc);
    private static DateTime T(double minutesFromKickoff) => Kickoff.AddMinutes(minutesFromKickoff);

    // ═══ AYRIŞTIRICILAR (gerçek resmî cevap) ═════════════════════════════════

    [Fact]
    public void SerieA_MacListesi_VeneziaFiorentina_KimlikVeSaat()
    {
        var records = SerieASdpSource.ParseMatches(Fixture("seriea_matches_md4.json"));
        var vf = records.Single(r => r.HomeName == "Venezia");
        Assert.Equal("Fiorentina", vf.AwayName);
        Assert.Equal(Kickoff, vf.KickoffUtc);
        Assert.Equal("serie-a::Football_Match::e32f1d77a1aa4798845a2d0239cbaf5a", vf.OfficialMatchId);
        Assert.StartsWith("https://www.legaseriea.it/serie-a/match/e32f1d77a1aa4798845a2d0239cbaf5a/", vf.OfficialUrl);
    }

    [Fact]
    public void SerieA_Kadro_GercekCevap_IkiTaraf11Oyuncu_DizilisVeTeknikDirektor()
    {
        var rec = SerieASdpSource.ParseMatches(Fixture("seriea_matches_md4.json")).Single(r => r.HomeName == "Venezia");
        var doc = SerieASdpSource.ParseLineup(Fixture("seriea_lineups_venezia_fiorentina.json"), rec,
            "https://api-sdp.legaseriea.it/v1/serie-a/football/seasons/x/matches/y/lineups", "h")!;

        Assert.Equal(11, doc.Home!.Starters.Count);
        Assert.Equal(11, doc.Away!.Starters.Count);
        Assert.Equal("3-5-2", doc.Home.Formation);
        Assert.Equal("4-3-3", doc.Away.Formation);
        Assert.Equal("Giovanni Stroppa", doc.Home.Coach);
        Assert.Equal("Paolo Vanoli", doc.Away.Coach);
        Assert.Contains(doc.Away.Starters, p => p.Name == "David de Gea" && p.ShirtNumber == 43 && p.Position == "G");
        Assert.True(doc.Home.Bench.Count > 0);
        // Grid resmî taktik koordinatlardan: kaleci 1. hat, 3-5-2 → 4 hat, her ilk 11 oyuncusunda.
        Assert.All(doc.Home.Starters, p => Assert.NotNull(p.Grid));
        Assert.Equal("1:1", doc.Home.Starters.Single(p => p.Position == "G").Grid);
        Assert.Equal(4, doc.Home.Starters.Select(p => p.Grid!.Split(':')[0]).Distinct().Count());

        var v = OfficialContentVerificationService.VerifyLineup(doc, "Venezia", "Fiorentina");
        Assert.True(v.BothSidesAccepted, $"{v.SourceRejectReason} {v.Home.Reason} {v.Away.Reason}");
    }

    [Fact]
    public void SerieA_KadroYayimlanmamis_BelgeYok()
    {
        var rec = new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "m", null, "Atalanta", "Cagliari",
            Kickoff, OfficialMatchStatuses.Scheduled, null, null, "UPCOMING");
        Assert.Null(SerieASdpSource.ParseLineup(Fixture("seriea_lineups_empty.json"), rec, "https://api-sdp.legaseriea.it/x", "h"));
    }

    [Fact]
    public void PremierLeague_Kadro_GercekCevap_IlkOnbirFormationdan()
    {
        var (records, _) = PremierLeagueSdpSource.ParseMatchesPage(Fixture("pl_matches_page.json"));
        var ars = records.Single(r => r.OfficialMatchId == "2645195");
        Assert.Equal("Arsenal", ars.HomeName);
        Assert.Equal(OfficialMatchStatuses.Finished, ars.Status);
        Assert.Equal(new DateTime(2026, 8, 21, 19, 0, 0, DateTimeKind.Utc), ars.KickoffUtc); // 20:00 BST

        var doc = PremierLeagueSdpSource.ParseLineup(Fixture("pl_lineups_arsenal_coventry.json"), ars,
            "https://sdp-prem-prod.premier-league-prod.pulselive.com/api/v3/matches/2645195/lineups", "h")!;
        Assert.Equal(11, doc.Home!.Starters.Count);
        Assert.Equal(11, doc.Away!.Starters.Count);
        Assert.Equal("4-2-3-1", doc.Home.Formation);
        Assert.Equal("Mikel Arteta", doc.Home.Coach);
        Assert.Contains(doc.Home.Starters, p => p.Name == "David Raya" && p.ShirtNumber == 1 && p.Grid == "1:1");
        Assert.Equal(5, doc.Home.Starters.Select(p => p.Grid!.Split(':')[0]).Distinct().Count()); // 4-2-3-1 = GK + 4 hat
        Assert.True(OfficialContentVerificationService.VerifyLineup(doc, "Arsenal", "Coventry").BothSidesAccepted);

        var upcoming = records.First(r => r.Status == OfficialMatchStatuses.Scheduled);
        Assert.Null(upcoming.HomeScore); // oynanmamış maçta skor uydurulmaz
        Assert.Null(PremierLeagueSdpSource.ParseLineup(Fixture("pl_lineups_empty.json"), upcoming,
            "https://sdp-prem-prod.premier-league-prod.pulselive.com/api/v3/matches/x/lineups", "h"));
    }

    [Fact]
    public void Tff_HaftaninMaclari_VeMacSayfasi_GercekHtml()
    {
        var now = new DateTime(2026, 9, 11, 20, 0, 0, DateTimeKind.Utc); // 23:00 TSİ
        var rows = TffSource.ParseWeekly(Fixture("tff_weekly.html"), now);
        var bjk = rows.Single(r => r.OfficialMatchId == "317827");
        Assert.Equal("BEŞİKTAŞ A.Ş.", bjk.HomeName);
        Assert.Equal("ERZURUMSPOR FK", bjk.AwayName);
        Assert.Equal(new DateTime(2026, 9, 11, 17, 0, 0, DateTimeKind.Utc), bjk.KickoffUtc); // 20:00 TSİ
        Assert.Equal(OfficialMatchStatuses.Finished, bjk.Status);
        Assert.Equal((3, 0), (bjk.HomeScore!.Value, bjk.AwayScore!.Value));
        Assert.True(rows.Count >= 9);

        // Skor maç başladıktan hemen sonra görünseydi kesin sonuç SAYILMAZDI (zaman kapısı).
        Assert.Equal(OfficialMatchStatuses.Unknown, TffSource.ResolveStatus(bjk.KickoffUtc, 1, 0, bjk.KickoffUtc!.Value.AddMinutes(60)));

        var page = TffSource.ParseMatchPage(Fixture("tff_match_besiktas_erzurumspor.html"))!;
        Assert.Equal(11, page.Home!.Starters.Count);
        Assert.Equal(11, page.Away!.Starters.Count);
        Assert.Equal("VINCENZO ITALIANO", page.Home.Coach);
        Assert.All(page.Home.Starters, p => Assert.Null(p.Grid)); // TFF konum vermiyor → uydurulmaz
        Assert.Equal((3, 0), (page.HomeScore!.Value, page.AwayScore!.Value));
        Assert.True(OfficialTeamNameMatcher.SameTeam(page.HomeName, "Beşiktaş"));
    }

    // ═══ KİMLİK ══════════════════════════════════════════════════════════════

    private static OfficialMatchRecord Rec(string home, string away, DateTime kickoff, string id = "m1")
        => new(OfficialSourceRegistry.SerieASdp, id, null, home, away, kickoff, OfficialMatchStatuses.Scheduled, null, null, null);

    private static FormaxMatchIdentity Fm(string home = "Venezia", string away = "Fiorentina")
        => new(15383, 135, home, away, Kickoff);

    [Fact]
    public void Kimlik_DogruTakimTarihYon_Kabul()
    {
        var d = OfficialMatchIdentityResolver.Resolve(Fm(), new[] { Rec("Venezia", "Fiorentina", Kickoff) });
        Assert.True(d.Accepted);
        Assert.Equal(TimeSpan.Zero, d.KickoffDelta);
    }

    [Fact]
    public void Kimlik_YanlisYon_Reddedilir()
    {
        var d = OfficialMatchIdentityResolver.Resolve(Fm(), new[] { Rec("Fiorentina", "Venezia", Kickoff) });
        Assert.False(d.Accepted);
        Assert.Equal(OfficialIdentityDecision.ReasonOrientationReversed, d.Reason);
    }

    [Fact]
    public void Kimlik_YanlisTarih_Reddedilir()
    {
        var d = OfficialMatchIdentityResolver.Resolve(Fm(), new[] { Rec("Venezia", "Fiorentina", Kickoff.AddDays(120)) });
        Assert.False(d.Accepted);
        Assert.Equal(OfficialIdentityDecision.ReasonKickoffOutOfWindow, d.Reason);
    }

    [Fact]
    public void Kimlik_YanlisTakim_Reddedilir()
    {
        var d = OfficialMatchIdentityResolver.Resolve(Fm(), new[] { Rec("Venezia", "Lazio", Kickoff) });
        Assert.False(d.Accepted);
        Assert.Equal(OfficialIdentityDecision.ReasonNoCandidate, d.Reason);
    }

    [Theory]
    [InlineData("BEŞİKTAŞ A.Ş.", "Beşiktaş", true)]
    [InlineData("ÇAYKUR RİZESPOR A.Ş.", "Rizespor", true)]
    [InlineData("ARCA ÇORUM FK", "Çorum FK", true)]
    [InlineData("GENÇLERBİRLİĞİ", "Gençlerbirliği S.K.", true)]
    [InlineData("İSTANBUL BAŞAKŞEHİR FK", "Başakşehir", true)]
    [InlineData("Brighton and Hove Albion", "Brighton", true)]
    [InlineData("Milan", "AC Milan", true)]
    [InlineData("Wolverhampton Wanderers", "Wolves", true)]
    [InlineData("Manchester City", "Manchester United", false)]
    [InlineData("Real Sociedad", "Real Madrid", false)]
    public void TakimAdiEslestirici(string official, string formax, bool same)
        => Assert.Equal(same, OfficialTeamNameMatcher.SameTeam(official, formax));

    // ═══ DOĞRULAMA ═══════════════════════════════════════════════════════════

    private static OfficialLineupSide Side(string team, int starters = 11, int bench = 5, bool dupe = false)
    {
        var s = Enumerable.Range(1, starters).Select(i => new OfficialLineupPlayer($"{team} P{i}", i, "M", false)).ToList();
        if (dupe) s[1] = s[0];
        var b = Enumerable.Range(starters + 1, bench).Select(i => new OfficialLineupPlayer($"{team} B{i}", i, "M", false)).ToList();
        return new OfficialLineupSide(team, "4-3-3", s, b, "Coach");
    }

    private static OfficialLineupDocument Doc(OfficialLineupSide? home, OfficialLineupSide? away,
        string source = OfficialSourceRegistry.SerieASdp, string url = "https://api-sdp.legaseriea.it/v1/x/lineups")
        => new(source, "m1", url, "hash", null, home, away);

    [Fact]
    public void Dogrulama_ResmiOlmayanKaynak_Reddedilir()
    {
        Assert.False(OfficialContentVerificationService.VerifyLineup(
            Doc(Side("Venezia"), Side("Fiorentina"), url: "https://www.flashscore.com/x"), "Venezia", "Fiorentina").AnySideAccepted);
        Assert.False(OfficialContentVerificationService.VerifyLineup(
            Doc(Side("Venezia"), Side("Fiorentina"), source: "ligue1-site", url: "https://ligue1.com/x"), "Venezia", "Fiorentina").AnySideAccepted);
        Assert.False(OfficialContentVerificationService.VerifyLineup(
            Doc(Side("Venezia"), Side("Fiorentina"), source: "unknown", url: "https://api-sdp.legaseriea.it/x"), "Venezia", "Fiorentina").AnySideAccepted);
    }

    [Fact]
    public void Dogrulama_EksikKadro_TamSayilmaz_DuplicateReddedilir_YanlisTakimReddedilir()
    {
        var v = OfficialContentVerificationService.VerifyLineup(Doc(Side("Venezia", starters: 10), Side("Fiorentina")), "Venezia", "Fiorentina");
        Assert.False(v.Home.Accepted);
        Assert.Equal(LineupSideVerdict.ReasonStarterCount, v.Home.Reason);
        Assert.True(v.Away.Accepted);
        Assert.False(v.BothSidesAccepted);

        var d = OfficialContentVerificationService.VerifyLineup(Doc(Side("Venezia", dupe: true), null), "Venezia", "Fiorentina");
        Assert.Equal(LineupSideVerdict.ReasonDuplicatePlayer, d.Home.Reason);
        Assert.Equal(LineupSideVerdict.ReasonMissing, d.Away.Reason);

        var w = OfficialContentVerificationService.VerifyLineup(Doc(Side("Lazio"), Side("Fiorentina")), "Venezia", "Fiorentina");
        Assert.Equal(LineupSideVerdict.ReasonWrongTeam, w.Home.Reason);
    }

    // ═══ TAKVİM ══════════════════════════════════════════════════════════════

    [Fact]
    public void Takvim_T60taBaslar_SlotlarVeKickoffArti10()
    {
        Assert.Equal(new[] { 60, 45, 30, 20, 15, 10, 5, -10 }, OfficialLineupSchedule.SlotMinutesBeforeKickoff);
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-61), false, null));
        Assert.True(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-60), false, null));
        // T−60 kontrolünden sonra T−45'e kadar yeni istek yok.
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-50), false, T(-60)));
        Assert.True(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-45), false, T(-60)));
        // Kaçırılan slotlar birikmez: T−12'de tek istek.
        Assert.True(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-12), false, T(-45)));
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-11), false, T(-12)));
        // Kickoff+10 son kontrol; +15 sonrası arama yok.
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(5), false, T(-5)));
        Assert.True(OfficialLineupSchedule.ShouldCheck(Kickoff, T(10), false, T(-5)));
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(16), false, T(-5)));
        // Kadro bulunduysa arama durur.
        Assert.False(OfficialLineupSchedule.ShouldCheck(Kickoff, T(-30), lineupComplete: true, null));
        Assert.Equal(LineupAvailability.Waiting, LineupAvailability.ResolveOfficial(false, Kickoff, T(-70)));
        Assert.Equal(LineupAvailability.SourceDelayed, LineupAvailability.ResolveOfficial(false, Kickoff, T(-20)));
        Assert.Equal(LineupAvailability.NotFound, LineupAvailability.ResolveOfficial(false, Kickoff, T(30)));
        Assert.Equal(LineupAvailability.Released, LineupAvailability.ResolveOfficial(true, Kickoff, T(120)));
    }

    // ═══ API-FOOTBALL KADRO İSTEĞİ = 0 ═══════════════════════════════════════

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    [Fact]
    public void ApiFootballKadroIstegi_YokTur_JobVeDiResmiToplayiciyaBagli()
    {
        var job = Source("Formax.Infrastructure", "BackgroundJobs", "LineupIngestionJob.cs");
        Assert.DoesNotContain("GetOfficialLineupAsync", job);
        Assert.DoesNotContain("LineupIngestionService>()", job);
        Assert.Contains("OfficialLineupCollector", job);

        var program = Source("Formax.API", "Program.cs");
        Assert.DoesNotContain("AddScoped<Formax.Infrastructure.Lineups.LineupIngestionService>", program);

        var collectorCtor = typeof(OfficialLineupCollector).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType).ToList();
        Assert.DoesNotContain(typeof(ISportsDataProvider), collectorCtor);
    }

    // ═══ TOPLAYICI (bellekte DB + sahte resmî kaynak) ════════════════════════

    internal sealed class FakeSource : IOfficialCompetitionSource
    {
        public string SourceKey { get; init; } = OfficialSourceRegistry.SerieASdp;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Lineup, OfficialPurposes.Schedule };
        public List<OfficialMatchRecord> Records { get; } = new();
        public Func<OfficialLineupDocument?> Next { get; set; } = () => null;
        public int FeedReads, LineupReads;

        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            FeedReads++;
            return Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Records, OfficialReadOutcomes.Ok, null, null));
        }

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            LineupReads++;
            return Task.FromResult(new OfficialRead<OfficialLineupDocument>(Next(), OfficialReadOutcomes.Ok, null, null));
        }
    }

    internal sealed class NoopFetcher : IOfficialContentFetcher
    {
        public Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("toplayıcı testinde ağ yok");
        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class CountingDelivery : INotificationService
    {
        public int Calls;
        public Task NotifyAsync(int matchId, string title, string message) { Calls++; return Task.CompletedTask; }
    }

    internal sealed class Env
    {
        private readonly string _name = $"olineup-{Guid.NewGuid():N}";
        public FakeSource Source { get; } = new();
        public CountingDelivery Delivery { get; } = new();
        public const int MatchId = 15383;

        public Env()
        {
            using var db = NewDb();
            db.Teams.Add(new Team { Id = 1, Name = "Venezia" });
            db.Teams.Add(new Team { Id = 2, Name = "Fiorentina" });
            db.Matches.Add(new Match
            {
                Id = MatchId, ExternalMatchId = "1550126", LeagueId = 135, League = "Serie A",
                MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2
            });
            db.SaveChanges();
            Source.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "sdp-vf", null,
                "Venezia", "Fiorentina", Kickoff, OfficialMatchStatuses.Scheduled, null, null, "UPCOMING"));
        }

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        public OfficialLineupCollector Collector(FormaxDbContext db, params IOfficialCompetitionSource[] sources)
            => new(db, sources.Length == 0 ? new IOfficialCompetitionSource[] { Source } : sources, new NoopFetcher(),
                   new MatchNotificationDispatcher(db, Delivery, NullLogger<MatchNotificationDispatcher>.Instance),
                   new ConfigurationBuilder().Build(), NullLogger<OfficialLineupCollector>.Instance);

        public async Task<LineupRoundReport> RunAsync(DateTime now)
        {
            using var db = NewDb();
            return await Collector(db).RunRoundAsync(now);
        }

        public void Follow(int userId, bool active = true)
        {
            using var db = NewDb();
            db.UserMatchFollows.Add(new UserMatchFollow { UserId = userId, MatchId = MatchId, IsActive = active });
            db.SaveChanges();
        }
    }

    private static OfficialLineupDocument Both() => Doc(Side("Venezia"), Side("Fiorentina"));

    [Fact]
    public async Task T60tanOnceAramaYok_T60taBaslar()
    {
        var env = new Env();
        await env.RunAsync(T(-61));
        Assert.Equal(0, env.Source.LineupReads);
        await env.RunAsync(T(-60));
        Assert.Equal(1, env.Source.LineupReads);
    }

    [Fact]
    public async Task BosCevap_BasariSayilmaz_SonrakiSlotAcik_SonKontrolYazilir()
    {
        var env = new Env();
        var r = await env.RunAsync(T(-60));
        Assert.Equal(LineupOutcomes.NotPublished, r.Outcomes.Single().Outcome);
        using (var db = env.NewDb())
        {
            var h = db.MatchLineups.Single();
            Assert.False(h.HomeLineupsReleased);
            Assert.Equal(T(-60), h.LastCheckedAtUtc);
            Assert.StartsWith("official:", h.Provider);
        }
        await env.RunAsync(T(-50)); // aynı slot: istek yok
        Assert.Equal(1, env.Source.LineupReads);
        env.Source.Next = Both;
        var second = await env.RunAsync(T(-45));
        Assert.Equal(LineupOutcomes.Released, second.Outcomes.Single().Outcome);
    }

    [Fact]
    public async Task KadroGelince_DbyeYazilir_Dogrulanir_AramaDurur()
    {
        var env = new Env();
        env.Source.Next = Both;
        await env.RunAsync(T(-30));
        using (var db = env.NewDb())
        {
            var h = db.MatchLineups.Single();
            Assert.True(h.HomeLineupsReleased && h.AwayLineupsReleased);
            Assert.Equal("Verified", h.VerificationStatus);
            Assert.Equal("seriea-sdp", h.SourceKey);
            Assert.Equal("https://api-sdp.legaseriea.it/v1/x/lineups", h.SourceUrl);
            Assert.Equal("hash", h.RawContentHash);
            Assert.NotNull(h.DiscoveredAtUtc);
            Assert.NotNull(h.VerifiedAtUtc);
            Assert.Equal(22, db.MatchLineupPlayers.Count(p => p.Role == "Starter"));
            Assert.Equal(10, db.MatchLineupPlayers.Count(p => p.Role == "Bench"));
        }
        await env.RunAsync(T(-20));
        await env.RunAsync(T(-5));
        Assert.Equal(1, env.Source.LineupReads); // kadro bulundu → arama durdu
    }

    [Fact]
    public async Task IkiResmiTarafKaynagi_Birlesir_DuplicateOyuncuYok()
    {
        var env = new Env();
        env.Source.Next = () => Doc(Side("Venezia"), null); // yalnız ev sahibi yayımladı
        var a = await env.RunAsync(T(-45));
        Assert.Equal(LineupOutcomes.PartiallyReleased, a.Outcomes.Single().Outcome);
        using (var db = env.NewDb())
        {
            Assert.True(db.MatchLineups.Single().HomeLineupsReleased);
            Assert.False(db.MatchLineups.Single().AwayLineupsReleased); // diğer taraf UYDURULMADI
            Assert.Equal("PartiallyVerified", db.MatchLineups.Single().VerificationStatus);
        }

        env.Source.Next = () => Doc(Side("Venezia"), Side("Fiorentina")); // ikinci açıklama
        var b = await env.RunAsync(T(-30));
        Assert.Equal(LineupOutcomes.Released, b.Outcomes.Single().Outcome);
        using var db2 = env.NewDb();
        var players = db2.MatchLineupPlayers.ToList();
        Assert.Equal(32, players.Count);
        Assert.Equal(players.Count, players.Select(p => (p.Side, p.PlayerName)).Distinct().Count());
    }

    [Fact]
    public async Task EksikKadro_TamKadroSayilmaz_BildirimYok()
    {
        var env = new Env();
        env.Follow(7);
        env.Source.Next = () => Doc(Side("Venezia", starters: 10), Side("Fiorentina", starters: 10));
        var r = await env.RunAsync(T(-30));
        Assert.Equal(LineupOutcomes.Rejected, r.Outcomes.Single().Outcome);
        using var db = env.NewDb();
        Assert.False(db.MatchLineups.Single().HomeLineupsReleased);
        Assert.Empty(db.MatchLineupPlayers);
        Assert.Empty(db.UserNotifications);
    }

    [Fact]
    public async Task Bildirim_TakipciyeTek_TakipEtmeyeneVeBirakanaYok_RestartTekrarUretmez()
    {
        var env = new Env();
        env.Follow(7);                  // aktif takipçi
        env.Follow(8, active: false);   // takibi bırakmış
        env.Source.Next = Both;

        await env.RunAsync(T(-30));
        using (var db = env.NewDb())
        {
            var n = Assert.Single(db.UserNotifications);
            Assert.Equal(7, n.UserId);
            Assert.Equal(MatchNotificationTypes.LineupAvailable, n.NotificationType);
            Assert.Equal("Kadrolar açıklandı", n.Title);
            Assert.Equal("Venezia–Fiorentina maçının resmî kadroları belli oldu. İlk 11’leri incele.", n.Message);
            Assert.Equal("/match/15383", n.Route);
            Assert.Equal("MATCH_LINEUP_AVAILABLE:15383:7", n.IdempotencyKey);
            Assert.NotNull(db.MatchLineups.Single().FollowersNotifiedAtUtc);
        }

        // "Restart": yeni toplayıcı + dağıtım bayrağı sıfırlanmış olsa bile ikinci bildirim yok.
        using (var db = env.NewDb())
        {
            db.MatchLineups.Single().FollowersNotifiedAtUtc = null;
            db.SaveChanges();
        }
        await env.RunAsync(T(-20));
        using (var db = env.NewDb()) Assert.Single(db.UserNotifications);

        // Doğrudan ikinci dağıtım da tekillik anahtarında durur.
        using (var db = env.NewDb())
        {
            var d = new MatchNotificationDispatcher(db, env.Delivery, NullLogger<MatchNotificationDispatcher>.Instance);
            var r = await d.DispatchAsync(new MatchNotificationRequest(Env.MatchId, MatchNotificationTypes.LineupAvailable,
                "x", "y", Formax.Domain.Enums.NotificationEventType.Lineup,
                u => MatchNotificationTypes.LineupKey(Env.MatchId, u), T(-10)));
            Assert.Equal(0, r.Created);
            Assert.Equal(1, r.SkippedDuplicate);
        }
        using (var db = env.NewDb()) Assert.Single(db.UserNotifications);
        Assert.Equal(1, env.Delivery.Calls);
    }

    [Fact]
    public async Task Bildirim_TercihKapaliysa_Yazilmaz_AmaKadroGorunur()
    {
        var env = new Env();
        env.Follow(7);
        using (var db = env.NewDb())
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = 7, PrefKey = "match:15383", Enabled = false });
            db.SaveChanges();
        }
        env.Source.Next = Both;
        await env.RunAsync(T(-30));
        using var db2 = env.NewDb();
        Assert.Empty(db2.UserNotifications);
        Assert.True(db2.MatchLineups.Single().HomeLineupsReleased);
    }

    [Fact]
    public async Task TakipEdilmeyenMacinKadrosuDaToplanir()
    {
        var env = new Env(); // takipçi yok
        env.Source.Next = Both;
        await env.RunAsync(T(-15));
        using var db = env.NewDb();
        Assert.True(db.MatchLineups.Single().HomeLineupsReleased);
        Assert.Empty(db.UserNotifications);
    }

    [Fact]
    public async Task KimlikKurulamazsa_KadroOkunmaz_KontrolSayilmaz()
    {
        var env = new Env();
        env.Source.Records.Clear();
        env.Source.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "rev", null,
            "Fiorentina", "Venezia", Kickoff, OfficialMatchStatuses.Scheduled, null, null, null));
        var r = await env.RunAsync(T(-30));
        Assert.Equal(LineupOutcomes.IdentityRejected, r.Outcomes.Single().Outcome);
        Assert.Equal(OfficialIdentityDecision.ReasonOrientationReversed, r.Outcomes.Single().Detail);
        Assert.Equal(0, env.Source.LineupReads);
        using var db = env.NewDb();
        Assert.Empty(db.MatchLineups);
    }

    [Fact]
    public async Task AyniTurdaMacListesiKaynakBasinaBirKezOkunur()
    {
        var env = new Env();
        using (var db = env.NewDb())
        {
            db.Teams.Add(new Team { Id = 3, Name = "Lazio" });
            db.Teams.Add(new Team { Id = 4, Name = "AC Milan" });
            db.Matches.Add(new Match { Id = 7913, LeagueId = 135, League = "Serie A", MatchDate = Kickoff.AddMinutes(10),
                Status = MatchStatuses.NotStarted, HomeTeamId = 3, AwayTeamId = 4 });
            db.SaveChanges();
        }
        env.Source.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "sdp-lm", null,
            "Lazio", "Milan", Kickoff.AddMinutes(10), OfficialMatchStatuses.Scheduled, null, null, null));
        var r = await env.RunAsync(T(-30));
        Assert.Equal(2, r.DueMatches);
        Assert.Equal(1, env.Source.FeedReads);
        Assert.Equal(2, env.Source.LineupReads);
    }

    [Fact]
    public async Task ResmiKaynagiOlmayanLig_IstekUretilmez()
    {
        var env = new Env();
        using (var db = env.NewDb())
        {
            db.Matches.Single().LeagueId = 61; // Ligue 1 — doğrulanmış kaynak yok
            db.SaveChanges();
        }
        var r = await env.RunAsync(T(-30));
        Assert.Equal(LineupOutcomes.NoOfficialSource, r.Outcomes.Single().Outcome);
        Assert.Equal(0, env.Source.FeedReads);
    }
}
