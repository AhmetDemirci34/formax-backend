using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Concurrency;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Notifications;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Formax.Infrastructure.PostMatch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KALICI GLOBAL VİDEO ARAMA + AYNI GÜN SONUÇ (15.09.2026). Gerçek internet YOK: resmî cevaplar kaydedilmiş fikstürlerden,
/// HTTP stub'dan ve InMemory DB'den gelir.
/// </summary>
public class GlobalVideoAndSameDayResultsTests
{
    private static readonly DateTime Now = new(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc);

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private static Match Finished(int id, DateTime kickoff, int league = 135, int home = 1, int away = 2, int hs = 3, int aws = 2)
        => new() { Id = id, HomeTeamId = home, AwayTeamId = away, MatchDate = kickoff, Status = MatchStatuses.Finished,
                   HomeScore = hs, AwayScore = aws, LeagueId = league, ExternalMatchId = "fx" + id };

    private static void SeedTeams(FormaxDbContext db)
    {
        db.Teams.Add(new Team { Id = 1, Name = "Frosinone" });
        db.Teams.Add(new Team { Id = 2, Name = "Venezia" });
        db.SaveChanges();
    }

    private sealed class StubCatalog : IOfficialVideoSourceCatalog
    {
        private readonly IReadOnlyList<OfficialVideoSource> _list;
        public StubCatalog(params OfficialVideoSource[] extra) => _list = OfficialVideoSources.All.Concat(extra).ToList();
        public IReadOnlyList<OfficialVideoSource> Current() => _list;
    }

    private sealed class StubProvider : IOfficialMatchVideoProvider
    {
        public List<OfficialVideoCandidate> Candidates { get; } = new();
        public string Name => "Stub";
        public int Priority => 1;
        public string Status => VideoProviderStatuses.Configured;
        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(VideoFixtureIdentity fixture, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfficialVideoCandidate>>(Candidates.ToList());
    }

    private sealed class Embed : IVideoEmbedVerifier
    {
        public bool Allow = true, Unavailable;
        public int Calls;
        public Task<EmbedVerification> VerifyAsync(OfficialVideoCandidate c, OfficialVideoSource s, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(Unavailable ? new EmbedVerification(false, null, null, "video kaldırılmış (oembed 404)", Unavailable: true)
                : Allow ? new EmbedVerification(true, "https://www.youtube-nocookie.com/embed/" + c.ExternalVideoId, null, "oembed 200")
                : new EmbedVerification(false, null, null, "yayıncı gömmeyi kapatmış (oembed 401)"));
        }
    }

    private static readonly OfficialVideoSource VeneziaSite = new("club:2", "Venezia FC", "Web", null, true, "site",
        OfficialVideoSourceTiers.Club, "Venezia", null, 2);
    private static readonly OfficialVideoSource AtalantaSite = new("club:9", "Atalanta", "Web", null, true, "site",
        OfficialVideoSourceTiers.Club, "Atalanta", null, 9);
    private static readonly OfficialVideoSource SerieASite = new("league:135", "Lega Serie A", "Web", null, true, "site",
        OfficialVideoSourceTiers.League, null, new[] { 135 });

    private static MatchVideoDiscoveryQueueService Queue(FormaxDbContext db, IOfficialMatchVideoProvider provider, Embed? embed = null,
        int batch = 400, int max = 10)
    {
        var catalog = new StubCatalog(VeneziaSite, SerieASite, AtalantaSite);
        var registrar = new MatchVideoRegistrar(db, embed ?? new Embed(), NullLogger<MatchVideoRegistrar>.Instance, catalog);
        return new MatchVideoDiscoveryQueueService(db, registrar, provider, catalog,
            Config(("PostMatch:Video:BackfillBatch", batch.ToString()), ("PostMatch:Video:MaxMatchesPerCycle", max.ToString())),
            NullLogger<MatchVideoDiscoveryQueueService>.Instance);
    }

    // ── 1. KAPSAM: BÜTÜN SONUÇLANMIŞ MAÇLAR, KOVA ÖNCELİĞİ, İMLEÇ ─────────────────────

    [Fact]
    public void YasKovasi_IstanbulGunSinirina_Gore_Bugun_Dun_7_30_Sezon_EskiSezon()
    {
        var now = new DateTime(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc);          // İstanbul 12:00
        Assert.Equal("Today", MatchVideoDiscoveryQueueService.Bucket(new DateTime(2026, 9, 14, 21, 30, 0, DateTimeKind.Utc), now)); // İst 15.09 00:30
        Assert.Equal("Yesterday", MatchVideoDiscoveryQueueService.Bucket(new DateTime(2026, 9, 14, 20, 30, 0, DateTimeKind.Utc), now)); // İst 14.09 23:30
        Assert.Equal("Last7Days", MatchVideoDiscoveryQueueService.Bucket(now.AddDays(-4), now));
        Assert.Equal("Last30Days", MatchVideoDiscoveryQueueService.Bucket(now.AddDays(-20), now));
        Assert.Equal("CurrentSeason", MatchVideoDiscoveryQueueService.Bucket(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc), now));
        Assert.Equal("OlderSeason", MatchVideoDiscoveryQueueService.Bucket(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), now));
    }

    [Fact]
    public async Task Backfill_UfukSiniriYok_KalıcıImlecleSayfaSayfa_RestartSonrasiKaldigiYerdenDevam()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name))
        {
            SeedTeams(db);
            // Bugün, dün, geçen ay, geçen sezon ve 2 yıl önce biten maçlar.
            db.Matches.AddRange(Finished(1, Now.AddHours(-4)), Finished(2, Now.AddHours(-26)), Finished(3, Now.AddDays(-40)),
                Finished(4, Now.AddDays(-200)), Finished(5, Now.AddDays(-400)), Finished(6, Now.AddDays(-800)),
                Finished(7, Now.AddDays(-801), league: 999));   // kilitli kapsam dışı: alınmaz
            db.SaveChanges();
            var (recent, backfill) = await Queue(db, new StubProvider(), batch: 2).EnqueueAsync(Now);
            Assert.Equal(2, recent);
            Assert.Equal(2, backfill);                             // ilk sayfa: 3 ve 4
            var cursor = db.VideoDiscoveryCursors.AsNoTracking().Single();
            Assert.Equal(4, cursor.LastMatchId);
        }

        using (var restarted = Db(name))                           // süreç yeniden başladı: imleç DB'de
        {
            var svc = Queue(restarted, new StubProvider(), batch: 2);
            Assert.Equal(2, (await svc.EnqueueAsync(Now)).Backfill);   // 5 ve 6 — baştan başlamadı
            Assert.Equal(0, (await svc.EnqueueAsync(Now)).Backfill);   // tur bitti
            var rows = restarted.MatchVideoDiscoveryQueue.AsNoTracking().ToDictionary(q => q.MatchId);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, rows.Keys.OrderBy(x => x));
            Assert.Equal("OlderSeason", rows[6].EnqueueReason);
            Assert.Equal(VideoDiscoveryStates.NotAvailableYet, rows[6].State);
            Assert.NotNull(rows[6].NextAttemptUtc);
            Assert.True(restarted.VideoDiscoveryCursors.AsNoTracking().Single().Pass >= 1);
        }
    }

    [Fact]
    public async Task ZamaniGelenler_EnYeniOnce_AmaEskiMacAcKalmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        for (var i = 0; i < 12; i++) db.Matches.Add(Finished(100 + i, Now.AddHours(-3 - i)));   // bugün/dün yoğun
        db.Matches.Add(Finished(200, Now.AddDays(-500)));
        db.SaveChanges();
        var svc = Queue(db, new StubProvider(), max: 10);
        await svc.EnqueueAsync(Now);

        var due = await svc.DueAsync(Now.AddDays(2), 10);
        Assert.Equal(10, due.Count);
        Assert.Equal(100, due[0].MatchId);                          // en yeni önce
        Assert.Contains(due, d => d.MatchId == 200);                // eski şerit payı
    }

    [Fact]
    public async Task VideoBulunanaDek_TerminalYok_PlanliTekrar_AyniMacIkiIsciyeVerilmez()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        db.Matches.Add(Finished(30, Now.AddDays(-60)));
        db.SaveChanges();
        var svc = Queue(db, new StubProvider());
        await svc.EnqueueAsync(Now);
        var at = Now;
        for (var k = 0; k < 25; k++)
        {
            Assert.True(await svc.TryClaimAsync(30, at));
            Assert.False(await svc.TryClaimAsync(30, at.AddSeconds(1)));
            await svc.ProcessAsync(30, at);
            at = db.MatchVideoDiscoveryQueue.AsNoTracking().Single().NextAttemptUtc!.Value;
        }
        var row = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
        Assert.Equal(VideoDiscoveryStates.NotAvailableYet, row.State);
        Assert.NotNull(row.NextAttemptUtc);
        Assert.Equal(25, row.AttemptCount);
    }

    // ── 2. YENİDEN DOĞRULAMA + OYNATICI HATASI ──────────────────────────────────────

    private static VideoFixtureIdentity FrosinoneVenezia(DateTime kickoff) => new(40, "fx40", kickoff, 1, 2, "Frosinone", "Venezia",
        Array.Empty<DateTime>(), 135, 3, 2);

    private static MatchVideo Row(int matchId, string id, string title, DateTime published, bool web, string type = MatchVideoTypes.MatchHighlights)
        => new()
        {
            MatchId = matchId, ExternalFixtureId = "fx" + matchId, ExternalVideoId = id, Title = title, OfficialPublisher = "Venezia FC",
            SourcePageUrl = "https://www.veneziafc.it/videos/" + id, EmbedUrl = "https://www.youtube-nocookie.com/embed/" + id,
            VideoType = type, PublishedAtUtc = published, IsOfficial = true, IsEmbeddable = true, CanPlayInApp = true,
            VerificationStatus = MatchVideoVerificationStatuses.Verified, MatchDateUtc = published.AddHours(-3), HomeTeamId = 1, AwayTeamId = 2,
            DiscoveryProvenance = web ? MatchVideoRules.OfficialWebProvenance : null,
            EvidencePageUrl = web ? "https://www.veneziafc.it/videos/" + id : null, EvidenceSourceKey = web ? "club:2" : null
        };

    private static MatchVideoRevalidationService Revalidation(FormaxDbContext db, Embed embed)
    {
        var catalog = new StubCatalog(VeneziaSite, SerieASite);
        return new MatchVideoRevalidationService(db, new MatchVideoRegistrar(db, embed, NullLogger<MatchVideoRegistrar>.Instance, catalog),
            embed, catalog, NullLogger<MatchVideoRevalidationService>.Instance);
    }

    [Fact]
    public async Task YenidenDogrulama_RssKanitliEskiKayit_Gizlenir_Reddedilir_MacKuyrugaDoner()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddDays(-2);
        db.Matches.Add(Finished(40, kickoff));
        db.MatchVideos.Add(Row(40, "rssOnly0001", "Serie A Enilive | Frosinone - Venezia FC 3-2 | Highlights", kickoff.AddHours(4), web: false));
        db.SaveChanges();
        Assert.False(MatchVideoRules.IsPlayable(db.MatchVideos.AsNoTracking().Single()));   // RSS kanıtı tek başına gösterilmez

        var report = await Revalidation(db, new Embed()).RunAsync(Now, 10);

        var v = db.MatchVideos.AsNoTracking().Single();
        Assert.Equal(MatchVideoVerificationStatuses.Rejected, v.VerificationStatus);
        Assert.Equal(MatchVideoRejectionReasons.RssOnlyEvidence, v.RejectionReason);
        Assert.Null(v.EmbedUrl);
        var q = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
        Assert.Equal("Revalidation:RssOnlyEvidence", q.RequeueReason);
        Assert.True(q.NextAttemptUtc <= Now);
        Assert.Contains(db.MatchVideoDiscoveryAttempts, a => a.RowKind == "Revalidation" && a.RejectionReason!.StartsWith("RssOnlyEvidence"));
        Assert.Equal(1, report.Requeued);
    }

    [Fact]
    public async Task YenidenDogrulama_RssKaydiResmiSitedeBulunursa_Korunur()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddDays(-2);
        db.Matches.Add(Finished(40, kickoff));
        db.MatchVideos.Add(Row(40, "l30ncafwuGk", "Serie A Enilive | Giornata 3 | Frosinone - Venezia FC 3-2 | Highlights", kickoff.AddHours(4), web: false));
        db.OfficialVideoSourceCatalog.Add(new OfficialVideoSourceRecord { Key = "club:2", Publisher = "Venezia FC", TeamId = 2, ClubName = "Venezia",
            Tier = OfficialVideoSourceTiers.Club, Status = "Candidate", WebsiteStatus = "Verified", Domain = "www.veneziafc.it", DiscoveredVia = "t", VerificationEvidence = "" });
        db.OfficialWebVideoEntries.Add(new OfficialWebVideoEntry { SourceKey = "club:2", PageUrl = "https://www.veneziafc.it/videos/x", PageUrlHash = "h",
            Title = "t", FoldedTitle = "t", YouTubeVideoId = "l30ncafwuGk", FirstSeenUtc = Now, LastSeenUtc = Now });
        db.SaveChanges();

        var report = await Revalidation(db, new Embed()).RunAsync(Now, 10);

        var v = db.MatchVideos.AsNoTracking().Single();
        Assert.True(MatchVideoRules.IsPlayable(v), v.VerificationNote);
        Assert.Equal("https://www.veneziafc.it/videos/x", v.EvidencePageUrl);
        Assert.Equal(1, report.Reinstated);
    }

    [Theory]
    [InlineData("Gaziantep FK - Fenerbahçe Maç Sonu Teknik Direktör Açıklamaları | Özet")]
    [InlineData("Frosinone - Venezia Özet #shorts")]
    [InlineData("Primavera 1 | Gli highlights di Frosinone-Venezia 3-2")]
    public async Task YenidenDogrulama_WebKanitliAmaYeniKuraliGecemeyen_Reddedilir(string title)
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddDays(-2);
        db.Matches.Add(Finished(40, kickoff));
        db.MatchVideos.Add(Row(40, "badvideo001", title, kickoff.AddHours(4), web: true));
        db.SaveChanges();

        await Revalidation(db, new Embed()).RunAsync(Now, 10);

        var v = db.MatchVideos.AsNoTracking().Single();
        Assert.Equal(MatchVideoVerificationStatuses.Rejected, v.VerificationStatus);
        Assert.Equal(MatchVideoRejectionReasons.FailedRevalidation, v.RejectionReason);
        Assert.NotNull(db.MatchVideoDiscoveryQueue.AsNoTracking().Single().NextAttemptUtc);
    }

    [Fact]
    public async Task YenidenDogrulama_KaldirilmisVideo_SourceBlocked_AramaSurer()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddDays(-2);
        db.Matches.Add(Finished(40, kickoff));
        db.MatchVideos.Add(Row(40, "removed0001", "Serie A Enilive | Frosinone - Venezia FC 3-2 | Highlights", kickoff.AddHours(4), web: true));
        db.SaveChanges();

        await Revalidation(db, new Embed { Unavailable = true }).RunAsync(Now, 10);

        var v = db.MatchVideos.AsNoTracking().Single();
        Assert.Equal(MatchVideoVerificationStatuses.SourceBlocked, v.VerificationStatus);
        var q = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
        Assert.Equal(VideoDiscoveryStates.SourceBlocked, q.State);
        Assert.NotNull(q.NextAttemptUtc);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(150)]
    [InlineData(152)]
    public async Task OynaticiHatasi_KayitSourceBlocked_AlternatifKalir_YoksaKuyrukAramayaDevam(int code)
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddDays(-1);
        db.Matches.Add(Finished(41, kickoff));
        db.MatchVideos.Add(Row(41, "firstvid001", "Frosinone - Venezia 3-2 Highlights", kickoff.AddHours(3), web: true));
        db.MatchVideos.Add(Row(41, "secondvid01", "Frosinone - Venezia 3-2 | Serie A Highlights", kickoff.AddHours(5), web: true));
        db.SaveChanges();
        var svc = new MatchVideoPlaybackReportService(db, NullLogger<MatchVideoPlaybackReportService>.Instance);

        var first = await svc.ReportAsync(41, "https://www.youtube-nocookie.com/embed/firstvid001", code, Now);
        Assert.True(first.Accepted);
        var rows = db.MatchVideos.AsNoTracking().ToDictionary(v => v.ExternalVideoId);
        Assert.Equal(MatchVideoVerificationStatuses.SourceBlocked, rows["firstvid001"].VerificationStatus);
        Assert.Equal(code, rows["firstvid001"].PlayerErrorCode);
        Assert.True(MatchVideoRules.IsPlayable(rows["secondvid01"]));              // alternatif resmî video
        Assert.Equal(VideoDiscoveryStates.FullHighlightsAvailable, first.QueueState);

        var second = await svc.ReportAsync(41, "secondvid01", code, Now.AddMinutes(1));
        Assert.Equal(VideoDiscoveryStates.SourceBlocked, second.QueueState);         // alternatif yok → dürüst durum
        Assert.NotNull(second.NextAttemptUtc);                                       // arka plan araması sürer
        Assert.Contains(db.MatchVideoDiscoveryAttempts, a => a.RowKind == "PlayerError" && a.ErrorType == "YT" + code);
        Assert.False((await svc.ReportAsync(41, "secondvid01", 99, Now)).Accepted);  // engel kodu olmayan hata kaydı kapatmaz
    }

    // ── 3. ROBOTS / NEZAKET KATMANI ─────────────────────────────────────────────────

    [Fact]
    public void RobotsRfc9309_EnUzunEslesme_Joker_4xxKisitsiz_5xxTamYasak()
    {
        var rules = RobotsTxtPolicy.ParseRules("User-agent: *\nDisallow: /en-*/*\nDisallow: /cache/\nAllow: /cache/public/\nDisallow: /*.php$\n");
        Assert.False(RobotsTxtPolicy.Allowed(rules, "/en-GB/laliga"));
        Assert.True(RobotsTxtPolicy.Allowed(rules, "/laliga-easports/resultados/2026-27/jornada-4"));
        Assert.False(RobotsTxtPolicy.Allowed(rules, "/cache/site/EredivisieNL/json/fixtures.json"));
        Assert.True(RobotsTxtPolicy.Allowed(rules, "/cache/public/x.json"));
        Assert.False(RobotsTxtPolicy.Allowed(rules, "/index.php"));
        Assert.True(RobotsTxtPolicy.Allowed(rules, "/index.php?x=1"));
        Assert.Equal(RobotsTxtPolicy.RobotsState.NoRestrictions, RobotsTxtPolicy.StateFor(403));
        Assert.Equal(RobotsTxtPolicy.RobotsState.NoRestrictions, RobotsTxtPolicy.StateFor(404));
        Assert.Equal(RobotsTxtPolicy.RobotsState.Unreachable, RobotsTxtPolicy.StateFor(503));
        Assert.Equal(RobotsTxtPolicy.RobotsState.Unreachable, RobotsTxtPolicy.StateFor(429));
        Assert.Equal(RobotsTxtPolicy.RobotsState.Unreachable, RobotsTxtPolicy.StateFor(null));
    }

    [Fact]
    public async Task SosyalYouTubeRss_Kapali_AgaHicIstekCikmaz()
    {
        var inner = new RecordingHandler(_ => (HttpStatusCode.OK, "<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><title>x</title></entry></feed>"));
        var provider = new Formax.Infrastructure.Social.Providers.YouTubeRssSocialProvider(new SingleClientFactory(inner),
            NullLogger<Formax.Infrastructure.Social.Providers.YouTubeRssSocialProvider>.Instance);

        var items = await provider.FetchAsync(new OfficialSocialAccount { Platform = "YouTube", Handle = "inter" });

        Assert.False(provider.IsEnabled);
        Assert.Empty(items);
        Assert.Empty(inner.Requests);
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly List<string> Requests = new();
        private readonly Func<Uri, (HttpStatusCode, string)> _route;
        public RecordingHandler(Func<Uri, (HttpStatusCode, string)> route) => _route = route;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests) Requests.Add(request.RequestUri!.ToString());
            var (code, body) = _route(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body), RequestMessage = request });
        }
    }

    private static HttpClient Polite(RecordingHandler inner, HostRateLimiter? limiter = null)
    {
        var polite = new PoliteHttpHandler(limiter ?? new HostRateLimiter(() => Now, (_, _) => Task.CompletedTask), new RobotsTxtPolicy(),
            NullLogger<PoliteHttpHandler>.Instance) { InnerHandler = inner };
        return new HttpClient(polite);
    }

    [Fact]
    public async Task NezaketKatmani_RobotsYasagindaIstekCikmaz_YouTubeAkisinaHicIstekYok()
    {
        var inner = new RecordingHandler(u => u.AbsolutePath == "/robots.txt"
            ? (HttpStatusCode.OK, "User-agent: *\nDisallow: /cache/\n")
            : (HttpStatusCode.OK, "ok"));
        using var client = Polite(inner);

        var blocked = await client.GetAsync("https://eredivisie.nl/cache/site/EredivisieNL/json/fixtures.json");
        Assert.Equal(451, (int)blocked.StatusCode);
        var allowed = await client.GetAsync("https://eredivisie.nl/competitie/wedstrijden/");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var feed = await client.GetAsync("https://www.youtube.com/feeds/videos.xml?channel_id=UCfYNqluOf8EbQkL44otydMw");
        Assert.Equal(451, (int)feed.StatusCode);

        Assert.DoesNotContain(inner.Requests, r => r.Contains("/cache/"));
        Assert.DoesNotContain(inner.Requests, r => r.Contains("/feeds/videos.xml"));
        Assert.DoesNotContain(inner.Requests, r => r.Contains("youtube.com"));   // ürün kuralı robots okumadan keser
    }

    [Fact]
    public async Task HostHizSiniri_Aralik_DevreKesici_5xxtaUstelGeriCekilmeliYenidenDeneme()
    {
        var clock = Now;
        var waits = new List<TimeSpan>();
        var limiter = new HostRateLimiter(() => clock, (t, _) => { waits.Add(t); clock += t; return Task.CompletedTask; });
        var calls = 0;
        var inner = new RecordingHandler(u => u.AbsolutePath == "/robots.txt" ? (HttpStatusCode.NotFound, "")
            : ++calls < 3 ? (HttpStatusCode.ServiceUnavailable, "") : (HttpStatusCode.OK, "ok"));
        using var client = Polite(inner, limiter);

        var res = await client.GetAsync("https://www.atalanta.it/sitemap-video.xml");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(3, calls);                                   // 2 yeniden deneme
        Assert.Contains(waits, w => w >= HostRateLimiter.DefaultMinInterval);

        for (var i = 0; i < 5; i++) limiter.RecordResult("www.atalanta.it", false);
        var open = await client.GetAsync("https://www.atalanta.it/video/x");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, open.StatusCode);   // devre açık: istek çıkmaz
    }

    // ── 4. RESMÎ SAYFA / AKIŞ AYRIŞTIRMA ────────────────────────────────────────────

    [Fact]
    public void VideoSitemap_GunHassasiyeti_Rss_Atom_SitemapIndex_Ayrisir()
    {
        var (items, children) = OfficialWebPageParser.ParseFeed(Fixture("legaseriea_sitemap_video.xml"));
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.False(i.Exact));                          // "2026-09-12" → gün hassasiyeti
        Assert.Empty(children);

        var index = "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap><loc>https://www.juventus.com/it/xml-sitemap/videos/2026-08.xml</loc><lastmod>2026-09-01</lastmod></sitemap></sitemapindex>";
        Assert.Single(OfficialWebPageParser.ParseFeed(index).Children);

        var rss = "<rss><channel><item><title>Frosinone-Venezia 3-2: gli highlights</title><link>https://angers-sco.fr/x</link><pubDate>Mon, 14 Sep 2026 18:00:00 GMT</pubDate></item></channel></rss>";
        var r = OfficialWebPageParser.ParseFeed(rss).Items.Single();
        Assert.True(r.Exact);
        var atom = "<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><title>Résumé Lens - Lyon</title><link href=\"https://www.rclens.fr/v\"/><updated>2026-09-14T20:00:00Z</updated></entry></feed>";
        Assert.Equal("https://www.rclens.fr/v", OfficialWebPageParser.ParseFeed(atom).Items.Single().PageUrl);
    }

    [Fact]
    public void ResmiVideoSayfasi_JsonLdVideoObject_Iframe_SameAs_Ayrisir()
    {
        var facts = OfficialWebPageParser.Parse(Fixture("venezia_video_page.html"), "https://www.veneziafc.it/videos/x");
        var (video, iframe, outcome) = OfficialWebPageParser.PrimaryVideo(facts, "Serie A Enilive | Giornata 3 | Frosinone - Venezia FC 3-2");
        Assert.Equal("Ok", outcome);
        Assert.Equal("l30ncafwuGk", video?.YouTubeId ?? iframe);
        Assert.Contains("@veneziafootballclub", facts.YouTubeHandles);

        var lsa = OfficialWebPageParser.Parse(Fixture("legaseriea_video_page.html"), "https://www.legaseriea.it/serie-a/videos/youtube/x");
        Assert.Contains("Q15804", lsa.WikidataIds);                                 // sitenin kendi sameAs kaydı
        var main = OfficialWebPageParser.PrimaryVideo(lsa, null).Video!;
        Assert.Equal("wf99QdY81tk", main.YouTubeId);
        Assert.True(main.UploadExact);

        // Birden çok video ve başlık eşleşmesi yok → hangisi maç videosu bilinemez.
        var multi = OfficialWebPageParser.Parse("<iframe src=\"https://www.youtube.com/embed/aaaaaaaaaaa\"></iframe><iframe src=\"https://www.youtube.com/embed/bbbbbbbbbbb\"></iframe>", "https://x.it/");
        Assert.Equal("Ambiguous", OfficialWebPageParser.PrimaryVideo(multi, "t").Outcome);
    }

    // ── 5. WEB SAĞLAYICI + TARAYICI ─────────────────────────────────────────────────

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _h;
        public Factory(HttpMessageHandler h) => _h = h;
        public HttpClient CreateClient(string name) => new(_h, disposeHandler: false);
    }

    [Fact]
    public async Task TarayiciVeSaglayici_ResmiSitemap_Sayfa_JsonLd_WebKanitliAdayUretir_IlgisizKulupKullanilmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        db.Matches.Add(Finished(50, kickoff));
        db.OfficialVideoSourceCatalog.AddRange(
            new OfficialVideoSourceRecord { Key = "club:2", Publisher = "Venezia FC", TeamId = 2, ClubName = "Venezia", Tier = OfficialVideoSourceTiers.Club,
                Status = "Candidate", WebsiteStatus = "Verified", Domain = "www.veneziafc.it", DiscoveredVia = "t", VerificationEvidence = "" },
            new OfficialVideoSourceRecord { Key = "club:9", Publisher = "Atalanta", TeamId = 9, ClubName = "Atalanta", Tier = OfficialVideoSourceTiers.Club,
                Status = "Candidate", WebsiteStatus = "Verified", Domain = "www.atalanta.it", DiscoveredVia = "t", VerificationEvidence = "" });
        db.OfficialWebFeeds.AddRange(
            new OfficialWebFeed { SourceKey = "club:2", Url = "https://www.veneziafc.it/sitemap-video.xml", Kind = "VideoSitemap", DiscoveredVia = "robots:Sitemap", NextFetchUtc = Now, CreatedAtUtc = Now },
            new OfficialWebFeed { SourceKey = "club:9", Url = "https://www.atalanta.it/sitemap-video.xml", Kind = "VideoSitemap", DiscoveredVia = "robots:Sitemap", NextFetchUtc = Now, CreatedAtUtc = Now },
            new OfficialWebFeed { SourceKey = "club:2", Url = "https://www.veneziafc.it/cache/videos.xml", Kind = "VideoSitemap", DiscoveredVia = "robots:Sitemap", NextFetchUtc = Now, CreatedAtUtc = Now });
        db.SaveChanges();

        var handler = new RecordingHandler(u =>
        {
            var s = u.ToString();
            if (u.AbsolutePath == "/robots.txt") return (HttpStatusCode.OK, "User-agent: *\nDisallow: /cache/\n");
            if (s == "https://www.veneziafc.it/sitemap-video.xml")
                return (HttpStatusCode.OK, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:video=\"http://www.google.com/schemas/sitemap-video/1.1\">" +
                    "<url><loc>https://www.veneziafc.it/videos/serie-a-enilive-giornata-3-frosinone-venezia-fc-3-2</loc><video:video><video:title>Serie A Enilive | Giornata 3 | Frosinone - Venezia FC 3-2</video:title><video:publication_date>2026-09-09</video:publication_date></video:video></url>" +
                    "<url><loc>https://www.veneziafc.it/videos/post-partita</loc><video:video><video:title>Post partita | Stroppa | Frosinone - Venezia FC</video:title><video:publication_date>2026-09-08</video:publication_date></video:video></url></urlset>");
            if (s == "https://www.atalanta.it/sitemap-video.xml")
                return (HttpStatusCode.OK, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:video=\"http://www.google.com/schemas/sitemap-video/1.1\"><url><loc>https://www.atalanta.it/video/frosinone-venezia</loc><video:video><video:title>Frosinone - Venezia 3-2 highlights</video:title><video:publication_date>2026-09-09</video:publication_date></video:video></url></urlset>");
            if (s.StartsWith("https://www.veneziafc.it/videos/serie-a")) return (HttpStatusCode.OK, Fixture("venezia_video_page.html"));
            return (HttpStatusCode.NotFound, "");
        });
        var polite = new PoliteHttpHandler(new HostRateLimiter(() => Now, (_, _) => Task.CompletedTask), new RobotsTxtPolicy(), NullLogger<PoliteHttpHandler>.Instance) { InnerHandler = handler };
        var crawler = new OfficialWebFeedCrawler(db, new Factory(polite), NullLogger<OfficialWebFeedCrawler>.Instance);

        var (feeds, fresh) = await crawler.CrawlDueAsync(Now, 10, CancellationToken.None);
        Assert.Equal(3, feeds);
        Assert.Equal(3, fresh);
        Assert.False(db.OfficialWebFeeds.AsNoTracking().Single(f => f.Url.Contains("/cache/")).IsActive);   // robots → adaptör kapandı
        Assert.DoesNotContain(handler.Requests, r => r.Contains("/cache/"));

        var provider = new OfficialWebVideoProvider(db, crawler, new VideoHttpBudget(Config()), Config(), NullLogger<OfficialWebVideoProvider>.Instance);
        var identity = FrosinoneVenezia(kickoff) with { MatchId = 50, ExternalFixtureId = "fx50" };
        var candidates = await provider.DiscoverAsync(identity);

        var c = Assert.Single(candidates);                                         // Atalanta sitesi bu maçın kulübü değil
        Assert.Equal("l30ncafwuGk", c.ExternalVideoId);
        Assert.Equal("club:2", c.SourceIdentifier);
        Assert.Equal(MatchVideoIdentityValidator.PrecisionExact, c.DatePrecision);  // JSON-LD uploadDate kesin
        Assert.StartsWith("https://www.veneziafc.it/videos/", c.EvidencePageUrl);
        Assert.DoesNotContain(handler.Requests, r => r.Contains("atalanta.it/video/"));

        // GERÇEK başlık (veneziafc.it, 15.09.2026) "highlights" demiyor ama YALNIZ karşılaşma + kayıtlı skor + turnuva/hafta:
        // yapısal özet işaretiyle kabul edilir (fazladan sözcüklü "Post partita | …" başlığı ise kabul edilmez).
        var verdict = MatchVideoIdentityValidator.Validate(c, identity, new StubCatalog(VeneziaSite).Current());
        Assert.True(verdict.Accepted, verdict.Reason);
        Assert.Equal(MatchVideoTypes.MatchHighlights, verdict.VideoType);
        Assert.False(MatchVideoIdentityValidator.Validate(c with { Title = "Post partita | Giovanni Stroppa | Frosinone - Venezia FC | Serie A Enilive" }, identity, new StubCatalog(VeneziaSite).Current()).Accepted);
        var marked = MatchVideoIdentityValidator.Validate(c with { Title = c.Title + " | Highlights" }, identity, new StubCatalog(VeneziaSite).Current());
        Assert.True(marked.Accepted, marked.Reason);
    }

    [Fact]
    public async Task Kayit_WebKanitliAday_ProvenanceYazilir_EskiRssSatiriAyniSatirdaEtkinlesir_AyniVideoIkiTurOlmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        db.Matches.Add(Finished(60, kickoff));
        var legacy = Row(60, "l30ncafwuGk", "Frosinone - Venezia FC 3-2 | Highlights", kickoff.AddHours(20), web: false);
        legacy.VerificationStatus = MatchVideoVerificationStatuses.Rejected; legacy.RejectionReason = MatchVideoRejectionReasons.RssOnlyEvidence; legacy.CanPlayInApp = false;
        db.MatchVideos.Add(legacy);
        db.SaveChanges();
        var registrar = new MatchVideoRegistrar(db, new Embed(), NullLogger<MatchVideoRegistrar>.Instance, new StubCatalog(VeneziaSite));
        var candidate = new OfficialVideoCandidate("YouTube", "club:2", "l30ncafwuGk", "Serie A Enilive | Giornata 3 | Frosinone - Venezia FC 3-2 | Highlights", null,
            new DateTime(2026, 9, 9, 13, 21, 27, DateTimeKind.Utc), "https://www.veneziafc.it/videos/x", null, null,
            DatePrecision: "Exact", EvidencePageUrl: "https://www.veneziafc.it/videos/x");

        var r = await registrar.RegisterAsync(60, candidate);

        Assert.True(r.Stored, r.Reason);
        var row = db.MatchVideos.AsNoTracking().Single();                         // yeni satır AÇILMADI
        Assert.True(MatchVideoRules.IsPlayable(row));
        Assert.Equal(MatchVideoRules.OfficialWebProvenance, row.DiscoveryProvenance);
        Assert.Equal("Duplicate", (await registrar.RegisterAsync(60, candidate)).Status);
        // Aynı video kimliği başka bir türle (uzun özet) ikinci kez yazılmaz.
        Assert.Equal("Duplicate", (await registrar.RegisterAsync(60, candidate with { Title = "Frosinone - Venezia 3-2 | Extended Highlights" })).Status);
        Assert.Single(db.MatchVideos.AsNoTracking());
    }

    // ── 6. KİMLİK: TARİH / SEZON / AYAK / TURNUVA / ALTYAPI / TÜR ─────────────────────

    private static IReadOnlyList<OfficialVideoSource> Sources => new StubCatalog(VeneziaSite, SerieASite, AtalantaSite).Current();

    private static OfficialVideoCandidate Web(string title, DateTime published, string source = "club:2", string precision = "Exact")
        => new("YouTube", source, "v" + Math.Abs(title.GetHashCode()), title, null, published, "https://www.veneziafc.it/videos/x", null, null,
            DatePrecision: precision, EvidencePageUrl: "https://www.veneziafc.it/videos/x");

    [Theory]
    [InlineData("Primavera 1 | Gli highlights di Frosinone-Venezia 3-2", "altyapı")]
    [InlineData("Coppa Italia | Frosinone - Venezia 3-2 | Highlights", "turnuva")]
    [InlineData("Frosinone - Venezia 3-2 | Cinematic Highlights", "montaj")]
    [InlineData("Conferenza stampa | Frosinone - Venezia | Highlights", "açıklama")]
    [InlineData("Frosinone - Venezia 1-0 | Highlights", "skor")]
    [InlineData("Venezia - Frosinone 2-3 | Highlights", "ters")]
    [InlineData("Frosinone - Venezia 3-2 | Highlights 2024/25", "sezon")]
    [InlineData("Frosinone - Venezia 3-2 Highlights #shorts", "kısa video")]
    public void Kimlik_YanlisMacIcerigi_Reddedilir(string title, string reason)
    {
        var kickoff = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var v = MatchVideoIdentityValidator.Validate(Web(title, kickoff.AddHours(6)), FrosinoneVenezia(kickoff), Sources);
        Assert.False(v.Accepted);
        Assert.Contains(reason, v.Reason);
    }

    [Fact]
    public void Kimlik_GunHassasiyeti_TarihsizBaglanti_IlgisizKaynak()
    {
        var kickoff = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var f = FrosinoneVenezia(kickoff);
        const string title = "Serie A Enilive | Frosinone - Venezia FC 3-2 | Highlights";
        Assert.True(MatchVideoIdentityValidator.Validate(Web(title, new DateTime(2026, 9, 9), precision: "Day"), f, Sources).Accepted);
        Assert.Contains("maç gününden önce", MatchVideoIdentityValidator.Validate(Web(title, new DateTime(2026, 9, 7), precision: "Day"), f, Sources).Reason);
        // Tarihsiz bağlantı: skor + yön zorunlu.
        Assert.False(MatchVideoIdentityValidator.Validate(Web("Frosinone - Venezia Highlights", Now, precision: "SeenOnly"), f, Sources).Accepted);
        // Başka kulübün sitesi bu maç için resmî kaynak değildir.
        Assert.Contains("kulübü/ligi değil", MatchVideoIdentityValidator.Validate(Web(title, kickoff.AddHours(6), source: "club:9"), f, Sources).Reason);
        // Lig sitesi kendi liginde geçerli.
        Assert.True(MatchVideoIdentityValidator.Validate(Web(title, kickoff.AddHours(6), source: "league:135"), f, Sources).Accepted);
    }

    [Fact]
    public void YapisalBaslik_YalnizKarsilasmaSkorTurnuva_KabulEdilir_FazladanSozcukReddedilir()
    {
        var kickoff = new DateTime(2026, 9, 13, 13, 0, 0, DateTimeKind.Utc);
        var atalanta = new VideoFixtureIdentity(99, "fx99", kickoff, 9, 10, "Atalanta", "Cagliari", Array.Empty<DateTime>(), 135, 1, 2);
        var cagliariSite = new OfficialVideoSource("club:10", "Cagliari", "Web", null, true, "site", OfficialVideoSourceTiers.Club, "Cagliari", null, 10);
        var sources = new StubCatalog(AtalantaSite, cagliariSite).Current();
        OfficialVideoCandidate C(string title, string src = "club:9") => new("YouTube", src, "v" + Math.Abs(title.GetHashCode()), title, null,
            new DateTime(2026, 9, 13), "https://www.atalanta.it/video/x", null, null, DatePrecision: "Day", EvidencePageUrl: "https://www.atalanta.it/video/x");

        // Gerçek atalanta.it sitemap başlıkları (15.09.2026).
        var ok = MatchVideoIdentityValidator.Validate(C("Atalanta-Cagliari 1-2 | 4ª Serie A Enilive 2026/27"), atalanta, sources);
        Assert.True(ok.Accepted, ok.Reason);
        Assert.Equal(MatchVideoTypes.MatchHighlights, ok.VideoType);
        Assert.False(MatchVideoIdentityValidator.Validate(C("Maurizio Sarri: \"Mi dispiace per i ragazzi\" | Atalanta-Cagliari 1-2 | 4ª Serie A Enilive 2026/27"), atalanta, sources).Accepted);
        Assert.False(MatchVideoIdentityValidator.Validate(C("Lazar Samardžić: \"Dobbiamo guardare avanti\" | Atalanta-Cagliari 1-2 | 4ª Serie A Enilive 2026/27"), atalanta, sources).Accepted);
        Assert.False(MatchVideoIdentityValidator.Validate(C("Atalanta-Cagliari 2-1 | 4ª Serie A Enilive 2026/27"), atalanta, sources).Accepted);   // skor tutmuyor
        Assert.False(MatchVideoIdentityValidator.Validate(C("Atalanta-Cagliari 1-2"), atalanta, sources).Accepted);                              // turnuva sözcüğü yok
        Assert.False(MatchVideoIdentityValidator.Validate(C("Primavera 1 | Gli highlights di Atalanta-Cagliari 1-2 | 4ª giornata 2026/27"), atalanta, sources).Accepted);
    }

    [Fact]
    public async Task NezaketKatmani_YonlendirmedeRobotsYenidenDenetlenir_RobotsIstegiUaTasir()
    {
        var uaSeen = new List<string>();
        var inner = new RecordingHandler(u => (HttpStatusCode.OK, ""));
        var handler = new UaHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/robots.txt")
            {
                lock (uaSeen) uaSeen.Add(req.Headers.UserAgent.ToString());
                return req.Headers.UserAgent.Count == 0 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: *\nDisallow: /private/\n") };
            }
            if (path == "/") return new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri("https://www.laliga.com/private/en-TR") } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });
        var polite = new PoliteHttpHandler(new HostRateLimiter(() => Now, (_, _) => Task.CompletedTask), new RobotsTxtPolicy(), NullLogger<PoliteHttpHandler>.Instance) { InnerHandler = handler };
        using var client = new HttpClient(polite);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FormaxVideoSourceCatalog/1.0");

        var res = await client.GetAsync("https://www.laliga.com/");

        Assert.Equal(451, (int)res.StatusCode);                             // yönlendirilen adres robots ile yasak → izlenmez
        Assert.DoesNotContain(handler.Paths, p => p.StartsWith("/private/"));
        Assert.All(uaSeen, ua => Assert.Contains("FormaxVideoSourceCatalog", ua));
    }

    private sealed class UaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public readonly List<string> Paths = new();
        public UaHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Paths) Paths.Add(request.RequestUri!.AbsolutePath);
            var r = _f(request); r.RequestMessage = request; return Task.FromResult(r);
        }
    }

    [Fact]
    public void BozukJsonLd_DenetimKarakteriVeKirikBlok_VideoObjectYineOkunur_WikipediaSiteBaglantisiEslesir()
    {
        var broken = "<script type=\"application/ld+json\">{\"@type\":\"VideoObject\",\"name\":\"Resumen Villarreal CF 1-2 Real Betis\",\"description\":\"satır\nsonu\",\"embedUrl\":\"https://www.youtube.com/embed/abcdefghijk\",\"uploadDate\":\"2026-09-14T22:10:00Z\"}</script>";
        var facts = OfficialWebPageParser.Parse(broken, "https://www.laliga.com/");
        Assert.Equal("abcdefghijk", Assert.Single(facts.Videos).YouTubeId);

        var truncated = "<script type=\"application/ld+json\">[{\"@type\":\"VideoObject\",\"name\":\"Gol de Baena\",\"embedUrl\":\"https://www.youtube.com/embed/zyxwvutsrqp\"}, {oops</script>";
        Assert.Equal("zyxwvutsrqp", Assert.Single(OfficialWebPageParser.Parse(truncated, "https://x.es/").Videos).YouTubeId);

        var item = OfficialVideoSourceDiscoveryService.ParseEntityData(
            "{\"entities\":{\"Q501245\":{\"labels\":{\"it\":{\"value\":\"Venezia FC\"}},\"claims\":{},\"sitelinks\":{\"itwiki\":{\"site\":\"itwiki\",\"title\":\"Venezia Football Club\",\"url\":\"https://it.wikipedia.org/wiki/Venezia_Football_Club\"}}}}}", "Q501245");
        Assert.True(OfficialVideoSourceDiscoveryService.SameWikipediaArticle("https://it.wikipedia.org/wiki/Venezia_Football_Club", item!.WikipediaUrls![0]));
        Assert.False(OfficialVideoSourceDiscoveryService.SameWikipediaArticle("https://it.wikipedia.org/wiki/Venezia", item.WikipediaUrls[0]));
    }

    [Fact]
    public async Task Tarayici_VideoEtiketsizDuzSitemap_VeCokBuyukGovde_AdaptoruKapatir()
    {
        using var db = Db(Guid.NewGuid().ToString());
        db.OfficialWebFeeds.AddRange(
            new OfficialWebFeed { SourceKey = "league:140", Url = "https://assets.laliga.com/sitemap/sitemap-videos-1.xml", Kind = "VideoSitemap", DiscoveredVia = "sitemap-index", NextFetchUtc = Now, CreatedAtUtc = Now },
            new OfficialWebFeed { SourceKey = "league:140", Url = "https://assets.laliga.com/sitemap/huge.xml", Kind = "VideoSitemap", DiscoveredVia = "sitemap-index", NextFetchUtc = Now, CreatedAtUtc = Now });
        db.SaveChanges();
        var handler = new UaHandler(req => req.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/sitemap/huge.xml" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[13_000_000]) },
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://www.laliga.com/es-AR/videos/x</loc></url></urlset>") }
        });
        var polite = new PoliteHttpHandler(new HostRateLimiter(() => Now, (_, _) => Task.CompletedTask), new RobotsTxtPolicy(), NullLogger<PoliteHttpHandler>.Instance) { InnerHandler = handler };
        var crawler = new OfficialWebFeedCrawler(db, new Factory(polite), NullLogger<OfficialWebFeedCrawler>.Instance);

        await crawler.CrawlDueAsync(Now, 10, CancellationToken.None);

        Assert.All(db.OfficialWebFeeds.AsNoTracking().ToList(), f => Assert.False(f.IsActive));
        Assert.Empty(db.OfficialWebVideoEntries);
    }

    [Fact]
    public void KulupBaglantisi_HostParcasiBaskaKulubeDeUyarsa_Kanit_Sayilmaz_AyniAlanAdiBekcisiDuzeltir()
    {
        var links = new List<(string, string)> { ("https://parisfc.fr/", ""), ("https://www.psg.fr/", "Paris Saint-Germain") };
        var league = new[] { "Paris FC", "Paris Saint Germain", "Lens" };
        Assert.Equal(new[] { "https://parisfc.fr/" }, OfficialVideoSourceDiscoveryService.LinksForTeam("Paris FC", links, league).ToArray());
        Assert.Equal(new[] { "https://www.psg.fr/" }, OfficialVideoSourceDiscoveryService.LinksForTeam("Paris Saint Germain", links, league).ToArray());

        var records = new List<OfficialVideoSourceRecord>
        {
            new() { Key = "club:4292", TeamId = 4292, Tier = OfficialVideoSourceTiers.Club, SourceKind = "Club", WebsiteStatus = "Verified", Domain = "parisfc.fr", OfficialWebsite = "https://parisfc.fr/", VerificationEvidence = "", Publisher = "Paris FC", DiscoveredVia = "t" },
            new() { Key = "club:5284", TeamId = 5284, Tier = OfficialVideoSourceTiers.Club, SourceKind = "Club", WebsiteStatus = "Verified", Domain = "parisfc.fr", OfficialWebsite = "https://parisfc.fr/", VerificationEvidence = "", Publisher = "PSG", DiscoveredVia = "t" },
            new() { Key = "broadcaster:www.asnl.net", Tier = OfficialVideoSourceTiers.Broadcaster, SourceKind = "Broadcaster", WebsiteStatus = "Verified", Domain = "www.asnl.net", VerificationEvidence = "", Publisher = "ASNL", DiscoveredVia = "t" },
            new() { Key = "broadcaster:www.trtspor.com.tr", Tier = OfficialVideoSourceTiers.Broadcaster, SourceKind = "Broadcaster", WebsiteStatus = "Verified", Domain = "www.trtspor.com.tr", VerificationEvidence = "", Publisher = "TRT SPOR", DiscoveredVia = "t" },
        };
        Assert.Equal(3, OfficialVideoSourceDiscoveryService.DemoteInconsistent(records, Now));
        Assert.All(records.Take(2), r => { Assert.Equal("Candidate", r.WebsiteStatus); Assert.Null(r.Domain); });
        Assert.Equal("Rejected", records[2].WebsiteStatus);
        Assert.Equal("Verified", records[3].WebsiteStatus);
    }

    // ── 7. GOL KLİBİ ────────────────────────────────────────────────────────────────

    private static VideoFixtureIdentity WithGoals(DateTime kickoff) => FrosinoneVenezia(kickoff) with
    {
        Goals = new[]
        {
            new FixtureGoal(12, null, "Joel Pohjanpalo", false, 0, 1, false, false),
            new FixtureGoal(30, null, "Farès Ghedjemis", true, 1, 1, false, false),
            new FixtureGoal(51, null, "Farès Ghedjemis", true, 2, 1, false, false),
            new FixtureGoal(77, null, "Kaan Kairinen", true, 3, 1, false, false),
            new FixtureGoal(90, 4, "Joel Pohjanpalo", false, 3, 2, false, true),
        }
    };

    [Fact]
    public void GolKlibi_KanonikGolcuVeAraSkorlaKabul_TamOzetSayilmaz()
    {
        var kickoff = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var f = WithGoals(kickoff);

        var v = MatchVideoIdentityValidator.Validate(Web("Il gol di Kairinen | Frosinone - Venezia 3-1", kickoff.AddHours(5)), f, Sources);
        Assert.True(v.Accepted, v.Reason);
        Assert.Equal(MatchVideoTypes.Goal, v.VideoType);
        Assert.Equal(77, v.Goal!.Minute);
        Assert.Equal("Kaan Kairinen", v.Goal.PlayerName);                 // oyuncu kanonik kayıttan

        // Aynı oyuncunun iki golü: ara skor hangisi olduğunu söyler.
        var second = MatchVideoIdentityValidator.Validate(Web("Gol Ghedjemis | Frosinone - Venezia 2-1", kickoff.AddHours(5)), f, Sources);
        Assert.Equal(51, second.Goal!.Minute);
        // Ara skor/dakika yoksa bağlanmaz.
        Assert.False(MatchVideoIdentityValidator.Validate(Web("Gol Ghedjemis | Frosinone - Venezia", kickoff.AddHours(5)), f, Sources).Accepted);
        // Golcü kanonik gollerde yok.
        Assert.Contains("golcü", MatchVideoIdentityValidator.Validate(Web("Il gol di Rossi | Frosinone - Venezia", kickoff.AddHours(5)), f, Sources).Reason);
        // Skor akışıyla çelişen ara skor.
        Assert.Contains("gol akışıyla", MatchVideoIdentityValidator.Validate(Web("Il gol di Kairinen | Frosinone - Venezia 1-0", kickoff.AddHours(5)), f, Sources).Reason);
        // Kanonik gol listesi yoksa gol klibi hiç kabul edilmez.
        Assert.False(MatchVideoIdentityValidator.Validate(Web("Il gol di Kairinen | Frosinone - Venezia 3-1", kickoff.AddHours(5)), FrosinoneVenezia(kickoff), Sources).Accepted);
    }

    // ── 8. AYNI GÜN SONUÇ ADAPTÖRLERİ ───────────────────────────────────────────────

    [Fact]
    public void LaLiga_ResmiSonucSayfasi_FullTimeSkorlu()
    {
        var (records, week, season) = LaLigaSiteSource.ParsePage(Fixture("laliga_resultados.html"));
        Assert.Equal(5, week);
        Assert.Equal(2026, season);
        var vb = records.Single(r => r.HomeName.Contains("Villarreal"));
        Assert.Equal(OfficialMatchStatuses.Finished, vb.Status);
        Assert.Equal((1, 2), (vb.HomeScore, vb.AwayScore));
        Assert.Equal(new DateTime(2026, 9, 14, 19, 0, 0, DateTimeKind.Utc), vb.KickoffUtc);
        Assert.True(OfficialMatchIdentityResolver.HomeMatches(vb, "Villarreal") && OfficialMatchIdentityResolver.AwayMatches(vb, "Real Betis"));
        var celta = records.Single(r => r.HomeName.Contains("Celta"));
        Assert.True(OfficialMatchIdentityResolver.HomeMatches(celta, "Celta Vigo") && OfficialMatchIdentityResolver.AwayMatches(celta, "Malaga"));
    }

    [Fact]
    public void TakimAdi_ACorunaIleLaCoruna_AyniTakim_AmaFarkliKuluplerKarismaz()
    {
        Assert.True(OfficialTeamNameMatcher.SameTeam("Real Club Deportivo de A Coruña SAD", "Deportivo La Coruna"));
        Assert.False(OfficialTeamNameMatcher.SameTeam("Real Club Deportivo de A Coruña SAD", "Deportivo Alaves"));
        Assert.False(OfficialTeamNameMatcher.SameTeam("Manchester City", "Manchester United"));
    }

    [Fact]
    public async Task EskiBitmemisMac_PencereDisindaKalmaz_10GuneKadarResmiSonuclaKapanir()
    {
        var name = Guid.NewGuid().ToString();
        var kickoff = new DateTime(2026, 9, 11, 19, 0, 0, DateTimeKind.Utc);
        using (var db = Db(name))
        {
            db.Teams.AddRange(new Team { Id = 1, Name = "Sevilla" }, new Team { Id = 2, Name = "Valencia" });
            db.Matches.Add(new Match { Id = 104455, LeagueId = 140, League = "La Liga", MatchDate = kickoff, Status = MatchStatuses.NotStarted,
                HomeTeamId = 1, AwayTeamId = 2, ExternalMatchId = "af-3" });
            db.SaveChanges();
        }
        var src = new ListSource();
        src.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.LaLigaSite, "102289", null, "Sevilla Fútbol Club SAD", "Valencia Club de Fútbol SAD",
            kickoff, OfficialMatchStatuses.Finished, 1, 0, "FullTime"));

        await Centre(name, src, new DateTime(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc));   // 3,5 gün sonra

        using var check = Db(name);
        var m = check.Matches.AsNoTracking().Single();
        Assert.Equal(MatchStatuses.Finished, m.Status);
        Assert.Equal((1, 0), (m.HomeScore, m.AwayScore));
    }

    [Fact]
    public void Ligue1_ResmiMacMerkezi_FullTimeSkorlu_PreMatchSkorsuz()
    {
        var finished = Ligue1ApiSource.ParseMatches(Fixture("ligue1_gameweek4.json"));
        Assert.Equal(3, finished.Count);
        Assert.All(finished, r => Assert.Equal(OfficialMatchStatuses.Finished, r.Status));
        Assert.All(finished, r => Assert.NotNull(r.HomeScore));
        var pre = Ligue1ApiSource.ParseMatches(Fixture("ligue1_gameweek5_prematch.json")).Single();
        Assert.Equal(OfficialMatchStatuses.Scheduled, pre.Status);
        Assert.Null(pre.HomeScore);
        Assert.Equal(OfficialMatchStatuses.Live, Ligue1ApiSource.MapStatus("secondHalf", true));
        Assert.Equal(new List<int> { 4, 5 }, Ligue1ApiSource.ParseNearestWeeks("{\"nearestGameWeeks\":{\"previousGameWeek\":{\"gameWeekNumber\":4},\"currentGameWeek\":{\"gameWeekNumber\":5}}}"));
    }

    [Fact]
    public void Efl_ResmiMacUcu_FullTimeSkorVeIlkYari_Sayfalama()
    {
        var (records, hasNext) = EflMultiClubSource.ParsePage(Fixture("efl_matches_page.json"));
        Assert.True(hasNext);
        var wolves = records.First();
        Assert.Equal(OfficialMatchStatuses.Finished, wolves.Status);
        Assert.NotNull(wolves.HomeScore);
        Assert.NotNull(wolves.HalfTimeHome);
        Assert.Equal(DateTimeKind.Utc, wolves.KickoffUtc!.Value.Kind);
        Assert.Equal(OfficialMatchStatuses.Scheduled, EflMultiClubSource.MapStatus("PreMatch"));
        Assert.Equal(OfficialMatchStatuses.Live, EflMultiClubSource.MapStatus("SecondHalf"));
    }

    [Fact]
    public void KayitDefteri_LaLigaLigue1EflDogrulanmis_EredivisieRobotsNedeniyleBlocked_YouTubeRssKapali()
    {
        Assert.NotEmpty(OfficialSourceRegistry.VerifiedFor(140, OfficialPurposes.Result));
        Assert.NotEmpty(OfficialSourceRegistry.VerifiedFor(61, OfficialPurposes.Result));
        Assert.NotEmpty(OfficialSourceRegistry.VerifiedFor(40, OfficialPurposes.Result));
        Assert.Empty(OfficialSourceRegistry.VerifiedFor(88, OfficialPurposes.Result));
        var ered = OfficialSourceRegistry.ByKey("eredivisie-site")!;
        Assert.Equal(OfficialSourceStatuses.Blocked, ered.Status);
        Assert.Contains("Disallow: /cache/", ered.EvidenceNote);
        Assert.Equal(OfficialSourceStatuses.Blocked, OfficialSourceRegistry.ByKey("youtube-official-channels")!.Status);
        Assert.False(OfficialSourceRegistry.IsAllowedHost("www.youtube.com"));
    }

    private sealed class ListSource : IOfficialCompetitionSource
    {
        public string SourceKey { get; init; } = OfficialSourceRegistry.LaLigaSite;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };
        public List<OfficialMatchRecord> Records { get; } = new();
        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Records, OfficialReadOutcomes.Ok, null, null));
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));
    }

    private static async Task<MatchCentreRoundReport> Centre(string dbName, IOfficialCompetitionSource source, DateTime now)
    {
        using var db = Db(dbName);
        var svc = new OfficialMatchCentreService(db, new[] { source },
            new MatchNotificationDispatcher(db, new OfficialLineupTests.CountingDelivery(), NullLogger<MatchNotificationDispatcher>.Instance),
            new ConfigurationBuilder().Build(), NullLogger<OfficialMatchCentreService>.Instance);
        return await svc.RunRoundAsync(now);
    }

    [Fact]
    public async Task AyniGunSonuc_BaslamisMacFinishedOlur_VideoKuyruguAcilir_CeliskiKorlemesineEzilmez()
    {
        var name = Guid.NewGuid().ToString();
        var kickoff = new DateTime(2026, 9, 14, 19, 0, 0, DateTimeKind.Utc);
        var now = kickoff.AddHours(2.5);
        using (var db = Db(name))
        {
            db.Teams.AddRange(new Team { Id = 1, Name = "Villarreal" }, new Team { Id = 2, Name = "Real Betis" },
                new Team { Id = 3, Name = "Celta Vigo" }, new Team { Id = 4, Name = "Malaga" });
            db.Matches.Add(new Match { Id = 104860, LeagueId = 140, League = "La Liga", MatchDate = kickoff, Status = MatchStatuses.NotStarted,
                HomeTeamId = 1, AwayTeamId = 2, ExternalMatchId = "af-1" });
            db.Matches.Add(new Match { Id = 15432, LeagueId = 140, League = "La Liga", MatchDate = kickoff.AddHours(-31), Status = MatchStatuses.Finished,
                HomeTeamId = 3, AwayTeamId = 4, HomeScore = 1, AwayScore = 1, ResultSource = "api-football:fixtures?date=2026-09-13", ExternalMatchId = "af-2" });
            db.SaveChanges();
        }
        var src = new ListSource();
        var (records, _, _) = LaLigaSiteSource.ParsePage(Fixture("laliga_resultados.html"));
        src.Records.AddRange(records);
        // Çelişki senaryosu: resmî kaynak Celta–Málaga'yı farklı yazsaydı.
        var celta = records.Single(r => r.HomeName.Contains("Celta"));
        src.Records.Remove(celta);
        src.Records.Add(celta with { HomeScore = 2, AwayScore = 1 });

        var report = await Centre(name, src, now);

        using (var db = Db(name))
        {
            var vb = db.Matches.AsNoTracking().Single(m => m.Id == 104860);
            Assert.Equal(MatchStatuses.Finished, vb.Status);                          // aynı gün: ertesi gün beklenmedi
            Assert.Equal((1, 2), (vb.HomeScore, vb.AwayScore));
            Assert.Equal("official:laliga-site", vb.ResultSource);
            var q = db.MatchVideoDiscoveryQueue.AsNoTracking().Single(x => x.MatchId == 104860);
            Assert.Equal("OfficialResult", q.RequeueReason);
            Assert.Equal(VideoDiscoveryStates.Searching, q.State);

            var cm = db.Matches.AsNoTracking().Single(m => m.Id == 15432);
            Assert.Equal((1, 1), (cm.HomeScore, cm.AwayScore));                       // ilk gözlemde ezilmedi
            Assert.Equal("Conflict", cm.ResultVerificationStatus);
            Assert.Contains(db.MatchResultObservations, o => o.MatchId == 15432 && o.Decision == "ConflictRecorded" && o.ExistingSource!.StartsWith("api-football"));
        }
        Assert.Contains(report.Matches, o => o.MatchId == 15432 && o.Outcome == "ResultConflict");

        await Centre(name, src, now.AddMinutes(10));                                    // ikinci resmî gözlem aynı skor
        using (var db = Db(name))
        {
            var cm = db.Matches.AsNoTracking().Single(m => m.Id == 15432);
            Assert.Equal((2, 1), (cm.HomeScore, cm.AwayScore));
            Assert.Contains(db.MatchResultObservations, o => o.MatchId == 15432 && o.Decision == "ConflictResolvedAfterConfirmation");
        }
    }

    // ── 9. SINIRLI ZAMANLAYICI ──────────────────────────────────────────────────────

    [Fact]
    public async Task SinirliZamanlayici_KuyrukDoluysaReddeder_SessizKayipYok_KapanistaBekleyenlerSonuclanir()
    {
        var scheduler = new DedicatedThreadWorkScheduler(1, "test-bounded", capacity: 2);
        var gate = new ManualResetEventSlim(false);
        var running = scheduler.RunAsync(() => { gate.Wait(); return 0; });
        await Task.Delay(100);
        var q1 = scheduler.RunAsync(() => 1);
        var q2 = scheduler.RunAsync(() => 2);
        var rejected = scheduler.RunAsync(() => 3);

        await Assert.ThrowsAsync<SchedulerSaturatedException>(() => rejected);
        Assert.Equal(1, scheduler.Rejected);
        Assert.Equal(2, scheduler.PeakQueued);

        scheduler.Dispose();                           // kapanış: bekleyenler iptal sonucu alır, kaybolmaz
        gate.Set();
        await running;
        var outcomes = await Task.WhenAll(new[] { q1, q2 }.Select(async t =>
        {
            try { return (await t).ToString(); } catch (SchedulerSaturatedException) { return "canceled"; }
        }));
        // Her kabul edilen iş ya sonucunu ya da açık bir iptal sonucunu aldı; hiçbiri askıda kalmadı.
        Assert.All(outcomes, o => Assert.True(o is "1" or "2" or "canceled"));
        Assert.True(q1.IsCompleted && q2.IsCompleted);
        await Assert.ThrowsAsync<SchedulerSaturatedException>(() => scheduler.RunAsync(() => 4));   // kapandıktan sonra ret
    }

    // ── 10. MAÇ SONU ANALİZİ: YETERSİZ VERİ UYDURULMAZ ─────────────────────────────

    [Fact]
    public async Task Analiz_YalnizSkorVarsa_YetersizVeri_Yazilir_EskiMacUfukSinirsizIslenir()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        db.Matches.Add(Finished(70, Now.AddDays(-900)));                  // 400 günden eski: yine işlenir
        db.Matches.Add(Finished(71, Now.AddDays(-2)));
        db.MatchEventRecords.AddRange(
            new MatchEventRecord { MatchId = 71, Minute = 12, EventType = "Goal", Detail = "Normal Goal", TeamName = "Venezia", PlayerName = "Joel Pohjanpalo", ExternalFixtureId = "fx71", ProviderEventId = "a", Source = "t" },
            new MatchEventRecord { MatchId = 71, Minute = 30, EventType = "Goal", Detail = "Normal Goal", TeamName = "Frosinone", PlayerName = "Farès Ghedjemis", ExternalFixtureId = "fx71", ProviderEventId = "b", Source = "t" },
            new MatchEventRecord { MatchId = 71, Minute = 51, EventType = "Goal", Detail = "Normal Goal", TeamName = "Frosinone", PlayerName = "Farès Ghedjemis", ExternalFixtureId = "fx71", ProviderEventId = "c", Source = "t" },
            new MatchEventRecord { MatchId = 71, Minute = 77, EventType = "Goal", Detail = "Normal Goal", TeamName = "Frosinone", PlayerName = "Kaan Kairinen", ExternalFixtureId = "fx71", ProviderEventId = "d", Source = "t" },
            new MatchEventRecord { MatchId = 71, Minute = 90, ExtraMinute = 4, EventType = "Goal", Detail = "Penalty", TeamName = "Venezia", PlayerName = "Joel Pohjanpalo", ExternalFixtureId = "fx71", ProviderEventId = "e", Source = "t" });
        db.SaveChanges();

        var svc = new PostMatchSummaryService(db, Config(("PostMatch:Summary:BackfillBatch", "10")), NullLogger<PostMatchSummaryService>.Instance);
        await svc.RunCycleAsync(Now);

        var rows = db.MatchPostMatchSummaries.AsNoTracking().ToDictionary(s => s.MatchId);
        Assert.Equal(PostMatchSummaryService.InsufficientGenerator, rows[70].Generator);
        Assert.Equal(string.Empty, rows[70].Text);
        Assert.Equal("Deterministic", rows[71].Generator);
        Assert.True(rows[71].Text.Split('\n').Length >= 2);

        // Kanonik gol listesi skor akışıyla kimliğe girer (gol klibi doğrulamasının girdisi).
        var registrar = new MatchVideoRegistrar(db, new Embed(), NullLogger<MatchVideoRegistrar>.Instance, new StubCatalog());
        var identity = await registrar.BuildIdentityAsync(71);
        Assert.Equal(5, identity!.Goals!.Count);
        Assert.Equal((3, 2), (identity.Goals[^1].HomeScoreAfter, identity.Goals[^1].AwayScoreAfter));
    }

    // ── 11. SAYFA AÇILIŞI KEŞİF BAŞLATMAZ ─────────────────────────────────────────

    [Fact]
    public void DetayVeSonucOkumaYolu_KesifTaramaYenidenDogrulamaCagirmaz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        var uc = File.ReadAllText(Path.Combine(dir!.FullName, "Formax.Application", "UseCases", "GetMatchDetailAIContextUseCase.cs"));
        foreach (var forbidden in new[] { "OfficialWebVideoProvider", "OfficialWebFeedCrawler", "MatchVideoRevalidationService", "OfficialVideoSourceDiscoveryService", "IOfficialMatchVideoProvider", "HttpClient" })
            Assert.DoesNotContain(forbidden, uc);
        var reader = File.ReadAllText(Path.Combine(dir.FullName, "Formax.Infrastructure", "PostMatch", "MatchVideoReader.cs"));
        Assert.DoesNotContain("HttpClient", reader);
        var playback = File.ReadAllText(Path.Combine(dir.FullName, "Formax.API", "Controllers", "MatchVideoPlaybackController.cs"));
        Assert.DoesNotContain("DiscoverAsync", playback);
    }
}
