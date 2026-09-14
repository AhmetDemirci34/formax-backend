using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Concurrency;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.PostMatch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KALICI VİDEO KEŞİF MOTORU + /detail EŞZAMANLILIK. Gerçek internet YOK: HTTP stub, InMemory DB, sahte saat.
/// </summary>
public class VideoDiscoveryEngineTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 19, 0, 0, DateTimeKind.Utc);

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private static Match Finished(int id, DateTime kickoff, int home = 1, int away = 2, int hs = 1, int aws = 2, int league = 39)
        => new() { Id = id, HomeTeamId = home, AwayTeamId = away, MatchDate = kickoff, Status = MatchStatuses.Finished,
                   HomeScore = hs, AwayScore = aws, LeagueId = league, ExternalMatchId = "fx" + id };

    private static void SeedTeams(FormaxDbContext db)
    {
        db.Teams.Add(new Team { Id = 1, Name = "Aston Villa" });
        db.Teams.Add(new Team { Id = 2, Name = "Nottingham Forest" });
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
        public int Calls;
        public string Name => "Stub";
        public int Priority => 1;
        public string Status => VideoProviderStatuses.Configured;
        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(VideoFixtureIdentity fixture, CancellationToken ct = default)
        { Interlocked.Increment(ref Calls); return Task.FromResult<IReadOnlyList<OfficialVideoCandidate>>(Candidates.ToList()); }
    }

    private sealed class Embed : IVideoEmbedVerifier
    {
        private readonly bool _allow;
        public Embed(bool allow) => _allow = allow;
        public Task<EmbedVerification> VerifyAsync(OfficialVideoCandidate c, OfficialVideoSource s, CancellationToken ct = default)
            => Task.FromResult(_allow
                ? new EmbedVerification(true, "https://www.youtube-nocookie.com/embed/" + c.ExternalVideoId, null, "oembed 200")
                : new EmbedVerification(false, null, null, "yayıncı gömmeyi kapatmış (oembed 401)"));
    }

    private static readonly OfficialVideoSource ForestClub = new("club:2", "Nottingham Forest FC", "YouTube",
        "UCyAxjuAr8f_BFDGCO3Htbxw", true, "test", OfficialVideoSourceTiers.Club, "Nottingham Forest", null, 2);

    private static MatchVideoDiscoveryQueueService Queue(FormaxDbContext db, StubProvider provider, bool embed = true, int backfill = 50)
    {
        var catalog = new StubCatalog(ForestClub);
        var registrar = new MatchVideoRegistrar(db, new Embed(embed), NullLogger<MatchVideoRegistrar>.Instance, catalog);
        return new MatchVideoDiscoveryQueueService(db, registrar, provider, catalog,
            Config(("PostMatch:Video:BackfillBatch", backfill.ToString()), ("PostMatch:Video:MaxMatchesPerCycle", "10")),
            NullLogger<MatchVideoDiscoveryQueueService>.Instance);
    }

    // ── KUYRUĞA ALMA ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BugunVeDunBitenMac_KuyruğaAlinir_OnceligiEskiMactanYuksektir()
    {
        var name = Guid.NewGuid().ToString();
        using var db = Db(name);
        SeedTeams(db);
        db.Matches.AddRange(Finished(10, Now.AddHours(-3)), Finished(11, Now.AddHours(-27)), Finished(12, Now.AddDays(-20)));
        db.SaveChanges();

        var (recent, backfill) = await Queue(db, new StubProvider()).EnqueueAsync(Now);

        Assert.Equal(2, recent);
        Assert.Equal(1, backfill);
        var rows = db.MatchVideoDiscoveryQueue.ToDictionary(q => q.MatchId);
        Assert.Equal("Today", rows[10].EnqueueReason);
        Assert.Equal("Yesterday", rows[11].EnqueueReason);
        Assert.Equal("Backfill", rows[12].EnqueueReason);
        Assert.Equal(VideoDiscoveryStates.Searching, rows[10].State);
        Assert.Equal(rows[10].EndUtc + TimeSpan.FromMinutes(15), rows[10].NextAttemptUtc);

        var due = await Queue(db, new StubProvider()).DueAsync(Now.AddDays(1), 10);
        Assert.Equal(new[] { 10, 11, 12 }, due.Select(d => d.MatchId));
    }

    [Fact]
    public async Task EskiVideosuzMaclar_SayfaSayfaKuyruğaAlinir_OynatilabilirVideosuOlanAlinmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        for (var i = 0; i < 5; i++) db.Matches.Add(Finished(100 + i, Now.AddDays(-10 - i)));
        db.MatchVideos.Add(new MatchVideo { MatchId = 104, ExternalVideoId = "v", Title = "t", OfficialPublisher = "p", SourcePageUrl = "u",
            VideoType = MatchVideoTypes.MatchHighlights, CanPlayInApp = true, ExternalFixtureId = "fx104" });
        db.SaveChanges();

        var svc = Queue(db, new StubProvider(), backfill: 2);
        Assert.Equal(2, (await svc.EnqueueAsync(Now)).Backfill);
        Assert.Equal(2, (await svc.EnqueueAsync(Now)).Backfill);
        Assert.Equal(0, (await svc.EnqueueAsync(Now)).Backfill);   // 104 videolu → alınmaz
        Assert.DoesNotContain(db.MatchVideoDiscoveryQueue, q => q.MatchId == 104);
    }

    // ── TUR, DURUM, RESTART ─────────────────────────────────────────────────

    [Fact]
    public async Task VideoYoksa_NotAvailableYet_TekrarPlanlanir_RestartSonrasiKuyrukKorunur()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name))
        {
            SeedTeams(db);
            db.Matches.Add(Finished(20, Now.AddDays(-3)));
            db.SaveChanges();
            var svc = Queue(db, new StubProvider());
            await svc.EnqueueAsync(Now);
            for (var k = 0; k < 9; k++)
            {
                var row = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
                var at = row.NextAttemptUtc!.Value;
                Assert.True(await svc.TryClaimAsync(20, at));
                await svc.ProcessAsync(20, at);
            }
        }

        using (var restarted = Db(name))
        {
            var row = restarted.MatchVideoDiscoveryQueue.Single();
            Assert.Equal(9, row.AttemptCount);
            Assert.Equal(VideoDiscoveryStates.NotAvailableYet, row.State);      // terminal DEĞİL
            Assert.NotNull(row.NextAttemptUtc);
            Assert.Null(row.LockedUntilUtc);
            Assert.True(restarted.MatchVideoDiscoveryAttempts.Count() >= 0);
        }
    }

    [Fact]
    public async Task TamOzetBulununca_TekrarDurur_GolKlibiTamOzetSayilmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddHours(-5);
        db.Matches.Add(Finished(30, kickoff));
        db.SaveChanges();

        var provider = new StubProvider();
        provider.Candidates.Add(new OfficialVideoCandidate("YouTube", ForestClub.YouTubeChannelId!, "goal1",
            "Igor Jesus goal | Aston Villa 1-2 Nottingham Forest | Premier League Highlights & goal", null,
            kickoff.AddHours(4), "https://www.youtube.com/watch?v=goal1", null, null));
        var svc = Queue(db, provider);
        await svc.EnqueueAsync(Now);
        Assert.True(await svc.TryClaimAsync(30, Now));
        await svc.ProcessAsync(30, Now);

        var row = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
        Assert.Equal(VideoDiscoveryStates.FullHighlightsAvailable, row.State);
        Assert.Null(row.NextAttemptUtc);
        var attempt = db.MatchVideoDiscoveryAttempts.Single(a => a.RowKind == "Candidate");
        Assert.True(attempt.Accepted);
        Assert.Equal("AwayClub", attempt.SourceKind);                          // Forest bu maçta deplasman
        Assert.Equal(MatchVideoTypes.MatchHighlights, attempt.VideoType);
        Assert.StartsWith("rss:channel_id=", attempt.SearchExpression);

        // Yalnız gol klibi → GoalClipsAvailable, tam özet değil ve arama sürer.
        Assert.Equal(VideoDiscoveryStates.GoalClipsAvailable, VideoDiscoveryStates.Resolve(false, true, false, false, 3));
        Assert.False(VideoDiscoveryStates.StopsRetrying(VideoDiscoveryStates.GoalClipsAvailable));
    }

    [Fact]
    public async Task GommeEngelliVideo_SourceBlocked_OynatilabilirSayilmaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        var kickoff = Now.AddHours(-5);
        db.Matches.Add(Finished(40, kickoff));
        db.SaveChanges();
        var provider = new StubProvider();
        provider.Candidates.Add(new OfficialVideoCandidate("YouTube", ForestClub.YouTubeChannelId!, "blocked1",
            "IGOR JESUS LATE WINNER! | Aston Villa 1-2 Nottingham Forest | Premier League Highlights", null,
            kickoff.AddHours(7), "https://www.youtube.com/watch?v=blocked1", null, null));
        var svc = Queue(db, provider, embed: false);
        await svc.EnqueueAsync(Now);
        Assert.True(await svc.TryClaimAsync(40, Now));
        await svc.ProcessAsync(40, Now);

        var row = db.MatchVideoDiscoveryQueue.AsNoTracking().Single();
        Assert.Equal(VideoDiscoveryStates.SourceBlocked, row.State);
        Assert.NotNull(row.NextAttemptUtc);                                    // alternatif resmî kaynak aranmaya devam
        Assert.False(db.MatchVideos.Single().CanPlayInApp);
    }

    [Fact]
    public async Task AyniMac_IkiIsciTarafindanAyniAndaAlinamaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        db.Matches.Add(Finished(50, Now.AddHours(-4)));
        db.SaveChanges();
        var svc = Queue(db, new StubProvider());
        await svc.EnqueueAsync(Now);

        Assert.True(await svc.TryClaimAsync(50, Now));
        Assert.False(await svc.TryClaimAsync(50, Now.AddMinutes(1)));
        Assert.True(await svc.TryClaimAsync(50, Now + MatchVideoDiscoveryQueueService.LockDuration + TimeSpan.FromSeconds(1)));
    }

    // ── TEKRAR PLANI ────────────────────────────────────────────────────────

    [Fact]
    public void TekrarPlani_15dk30dk60dk2sa4sa8sa12sa24sa_SonraGunluk_SonraHaftalik()
    {
        var end = Now;
        var expected = new[] { 15, 30, 60, 120, 240, 480, 720, 1440 }.Select(m => end.AddMinutes(m)).ToList();
        for (var k = 0; k < 8; k++) Assert.Equal(expected[k], VideoDiscoverySchedule.PlannedAt(end, k));
        Assert.Equal(end.AddHours(24).AddDays(1), VideoDiscoverySchedule.PlannedAt(end, 8));
        Assert.Equal(end.AddHours(24).AddDays(7), VideoDiscoverySchedule.PlannedAt(end, 14));
        Assert.Equal(end.AddHours(24).AddDays(14), VideoDiscoverySchedule.PlannedAt(end, 15));

        // Geçmiş maç: plan anları geçmiş olsa da denemeler art arda yapılmaz.
        var old = Now.AddDays(-60);
        var next = VideoDiscoverySchedule.NextAttempt(old, 5, Now);
        Assert.Equal(Now + VideoDiscoverySchedule.Gap(5), next);
        Assert.True(VideoDiscoverySchedule.Gap(1) >= TimeSpan.FromMinutes(15));
    }

    // ── HOST HIZ SINIRI + DEVRE KESİCİ + ROBOTS ─────────────────────────────

    [Fact]
    public async Task HostHizSiniri_AraliklariKorur_ArdisikHatadaDevreAcilir()
    {
        var clock = Now;
        var waited = new List<TimeSpan>();
        var limiter = new HostRateLimiter(() => clock, (t, _) => { waited.Add(t); clock += t; return Task.CompletedTask; });

        using (await limiter.AcquireAsync("www.example-club.com", CancellationToken.None)) { }
        using (await limiter.AcquireAsync("www.example-club.com", CancellationToken.None)) { }
        Assert.Single(waited);
        Assert.Equal(HostRateLimiter.DefaultMinInterval, waited[0]);

        for (var i = 0; i < 5; i++) limiter.RecordResult("www.example-club.com", false);
        Assert.True(limiter.IsOpen("www.example-club.com", out _));
        Assert.Null(await limiter.AcquireAsync("www.example-club.com", CancellationToken.None));
    }

    [Fact]
    public void RobotsTxt_DisallowUygulanir_YayimlanmisApiUclariMuaftir()
    {
        var rules = RobotsTxtPolicy.Parse("User-agent: Googlebot\nDisallow: /\n\nUser-agent: *\nDisallow: /api/\nDisallow: /preview/\n");
        Assert.False(RobotsTxtPolicy.Allowed(rules, "/api/matches"));
        Assert.True(RobotsTxtPolicy.Allowed(rules, "/"));
        Assert.True(RobotsTxtPolicy.IsPublishedApi(new Uri("https://www.youtube.com/oembed?url=x")));
        Assert.False(RobotsTxtPolicy.IsPublishedApi(new Uri("https://www.avfc.co.uk/")));
    }

    // ── KAYNAK KATALOĞU OTOMATİK GENİŞLER (Wikidata + resmî site kanıtı) ─────

    private sealed class StubHttp : HttpMessageHandler
    {
        private readonly Func<Uri, (HttpStatusCode, string)> _route;
        public StubHttp(Func<Uri, (HttpStatusCode, string)> route) => _route = route;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _route(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _h;
        public Factory(HttpMessageHandler h) => _h = h;
        public HttpClient CreateClient(string name) => new(_h, disposeHandler: false);
    }

    private static string Sparql(params (string Qid, string Label, string Yts, string Site)[] rows)
        => "{\"results\":{\"bindings\":[" + string.Join(",", rows.Select(r =>
            $"{{\"club\":{{\"value\":\"http://www.wikidata.org/entity/{r.Qid}\"}},\"clubLabel\":{{\"value\":\"{r.Label}\"}},\"yts\":{{\"value\":\"{r.Yts}\"}},\"website\":{{\"value\":\"{r.Site}\"}}}}")) + "]}}";

    [Fact]
    public async Task Katalog_WikidataVeResmiSiteKanitiyla_KendiligindenGenisler_TekKanitDogrulamaz()
    {
        using var db = Db(Guid.NewGuid().ToString());
        SeedTeams(db);
        db.Teams.Add(new Team { Id = 3, Name = "Hull City" });
        db.Matches.Add(new Match { Id = 60, HomeTeamId = 1, AwayTeamId = 2, LeagueId = 39, MatchDate = Now.AddDays(-2), Status = "Finished" });
        db.Matches.Add(new Match { Id = 61, HomeTeamId = 3, AwayTeamId = 1, LeagueId = 39, MatchDate = Now.AddDays(-9), Status = "Finished" });
        db.SaveChanges();

        const string villaUc = "UCICNP0mvtr0prFwGUQIABfQ";
        const string forestUc = "UCyAxjuAr8f_BFDGCO3Htbxw";
        const string hullUc = "UCaaaaaaaaaaaaaaaaaaaaaa";
        var http = new StubHttp(uri =>
        {
            var u = uri.ToString();
            if (u.Contains("query.wikidata.org") && u.Contains("VALUES"))
                return (HttpStatusCode.OK, Sparql(("Q9448", "Premier League", "", "")));
            if (u.Contains("query.wikidata.org"))
                return (HttpStatusCode.OK, Sparql(("Q18711", "Aston Villa F.C.", villaUc, "https://www.avfc.co.uk/"),
                                                  ("Q19490", "Nottingham Forest F.C.", "", "https://nottinghamforest.co.uk"),
                                                  ("Q19477", "Hull City A.F.C.", "", "https://www.wearehullcity.co.uk/")));
            if (u.StartsWith("https://www.avfc.co.uk")) return (HttpStatusCode.OK, "<a href=\"https://www.youtube.com/user/avfcofficial\">YT</a>");
            if (u.StartsWith("https://nottinghamforest.co.uk")) return (HttpStatusCode.OK, "<a href=\"https://youtube.com/@NottinghamForestFC\">YT</a>");
            if (u.StartsWith("https://www.wearehullcity.co.uk")) return (HttpStatusCode.OK, $"<a href=\"https://www.youtube.com/channel/{hullUc}\">YT</a>");
            if (u.Contains("/user/avfcofficial")) return (HttpStatusCode.OK, $"<link rel=\"canonical\" href=\"https://www.youtube.com/channel/{villaUc}\">");
            if (u.Contains("/@NottinghamForestFC")) return (HttpStatusCode.OK, $"<link rel=\"canonical\" href=\"https://www.youtube.com/channel/{forestUc}\">");
            if (u.Contains("channel_id=" + villaUc)) return (HttpStatusCode.OK, "<feed xmlns=\"http://www.w3.org/2005/Atom\"><author><name>Aston Villa Football Club</name></author></feed>");
            if (u.Contains("channel_id=" + forestUc)) return (HttpStatusCode.OK, "<feed xmlns=\"http://www.w3.org/2005/Atom\"><author><name>Nottingham Forest FC</name></author></feed>");
            if (u.Contains("channel_id=" + hullUc)) return (HttpStatusCode.OK, "<feed xmlns=\"http://www.w3.org/2005/Atom\"><author><name>Random Football Edits</name></author></feed>");
            return (HttpStatusCode.NotFound, "");
        });

        var catalog = new OfficialVideoSourceCatalog(new NullScopeFactory());
        var svc = new OfficialVideoSourceDiscoveryService(db, new Factory(http), catalog, NullLogger<OfficialVideoSourceDiscoveryService>.Instance);
        var report = await svc.RunAsync(Now, 40);

        var rows = db.OfficialVideoSourceCatalog.AsNoTracking().ToDictionary(r => r.Key);
        Assert.Equal("Verified", rows["club:1"].Status);                       // Wikidata + site + yazar
        Assert.Equal(villaUc, rows["club:1"].YouTubeChannelId);
        Assert.Equal("Verified", rows["club:2"].Status);                       // Wikidata'da kanal yok: site + yazar
        Assert.Equal(forestUc, rows["club:2"].YouTubeChannelId);
        Assert.Equal("Candidate", rows["club:3"].Status);                      // tek kanıt (site) + yabancı yazar adı
        Assert.True(report.Verified >= 2);

        var sources = OfficialVideoSourceCatalog.Merge(OfficialVideoSources.All, rows.Values);
        Assert.Contains(sources, s => s.YouTubeChannelId == forestUc && s.TeamId == 2);
        Assert.DoesNotContain(sources, s => s.YouTubeChannelId == hullUc);
    }

    private sealed class NullScopeFactory : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory
    {
        public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope() => throw new InvalidOperationException("testte kullanılmaz");
    }

    // ── KİMLİK KAPISI: SAHTE KANAL, FARKLI SEZON, OYUN/TEPKİ VİDEOSU ────────

    private static VideoFixtureIdentity Villa() => new(20370, "1557397", new DateTime(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc),
        1, 2, "Aston Villa", "Nottingham Forest", Array.Empty<DateTime>(), 39, 1, 2);

    private static OfficialVideoCandidate Cand(string title, string channel = "UCyAxjuAr8f_BFDGCO3Htbxw", int hoursAfter = 7)
        => new("YouTube", channel, "id" + Math.Abs(title.GetHashCode()), title, null,
            new DateTime(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc).AddHours(hoursAfter), "https://www.youtube.com/watch?v=x", null, null);

    [Theory]
    [InlineData("Aston Villa 1-2 Nottingham Forest | Premier League Highlights", "UCfakefakefakefakefakefa", "izin listesinde değil")]
    [InlineData("Aston Villa 1-2 Nottingham Forest | Highlights 2024/25", null, "sezon")]
    [InlineData("Aston Villa v Nottingham Forest | FC 26 Simulation Highlights", null, "oyun")]
    [InlineData("Liam Delap's Reaction | Aston Villa vs Nottingham Forest | Highlights", null, "oyun")]
    public void SahteKanal_FarkliSezon_OyunVeTepkiVideosu_Reddedilir(string title, string? channel, string reason)
    {
        var sources = new StubCatalog(ForestClub).Current();
        var v = MatchVideoIdentityValidator.Validate(Cand(title, channel ?? ForestClub.YouTubeChannelId!), Villa(), sources);
        Assert.False(v.Accepted);
        Assert.Contains(reason, v.Reason);
    }

    [Fact]
    public void KontrolluTakmaAd_NottmForest_TanınırAmaTahminiEslemeYapilmaz()
    {
        var sources = new StubCatalog(ForestClub).Current();
        var ok = MatchVideoIdentityValidator.Validate(Cand("Villa 1-2 Nott'm Forest | Premier League Highlights"), Villa(), sources);
        Assert.True(ok.Accepted, ok.Reason);
        Assert.True(TeamNameAliases.Mentions(MatchVideoIdentityValidator.Fold("Man Utd 0-1 Man City"), "Manchester United"));
        Assert.False(TeamNameAliases.Mentions(MatchVideoIdentityValidator.Fold("United 0-1 City"), "Manchester United"));
    }

    // ── /detail EŞZAMANLILIK REGRESYONU ─────────────────────────────────────

    [Fact]
    public async Task BloklayanIs_OzelIsParcaciginda_ThreadPoolAcKalmaz_AsyncLocalKorunur()
    {
        using var scheduler = new DedicatedThreadWorkScheduler(4, "test-detail");
        var local = new AsyncLocal<string?> { Value = "cid-1" };
        var seen = new List<string?>();

        var works = Enumerable.Range(0, 40).Select(_ => scheduler.RunAsync(() =>
        {
            Thread.Sleep(50);                       // senkron EF'i temsil eden bloklama
            lock (seen) seen.Add(local.Value);
            return Thread.CurrentThread.IsThreadPoolThread;
        })).ToList();

        // Bloklayan 40 iş sürerken thread pool'a atılan iş hemen başlamalı (açlık yok).
        var sw = Stopwatch.StartNew();
        await Task.Run(() => { });
        Assert.True(sw.ElapsedMilliseconds < 500, $"pool probu {sw.ElapsedMilliseconds} ms bekledi");

        var ranOnPool = await Task.WhenAll(works);
        Assert.All(ranOnPool, onPool => Assert.False(onPool));
        Assert.All(seen, v => Assert.Equal("cid-1", v));
    }

    [Fact]
    public async Task IptalEdilenIstek_BaslamadanDuser_IsCalismaz()
    {
        using var scheduler = new DedicatedThreadWorkScheduler(1, "test-cancel");
        var gate = new ManualResetEventSlim(false);
        var blocker = scheduler.RunAsync(() => { gate.Wait(); return 1; });
        using var cts = new CancellationTokenSource();
        var ran = false;
        var queued = scheduler.RunAsync(() => { ran = true; return 2; }, cts.Token);
        cts.Cancel();
        gate.Set();
        await blocker;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.False(ran);
    }

    [Fact]
    public void SapmaSnapshotJob_KendiIsParcaciginda_DetayIseKuyrukBeklemesiOlculur()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        var job = File.ReadAllText(Path.Combine(dir!.FullName, "Formax.Infrastructure", "BackgroundJobs", "SapmaSnapshotJob.cs"));
        var uc = File.ReadAllText(Path.Combine(dir.FullName, "Formax.Application", "UseCases", "GetMatchDetailAIContextUseCase.cs"));
        Assert.Contains("TaskCreationOptions.LongRunning", job);
        Assert.Contains("_blocking.RunAsync(", uc);
        Assert.DoesNotContain("MatchVideoDiscoveryQueueService", uc);          // sayfa açılışı keşif başlatmaz
        Assert.DoesNotContain("OfficialVideoSourceDiscoveryService", uc);
    }
}
