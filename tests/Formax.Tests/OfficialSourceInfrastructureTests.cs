using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Application.UseCases;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ORTAK RESMÎ KAYNAK ALTYAPISI — indiricinin güvenlik ve ekonomi sözleşmesi.
/// GERÇEK AĞ YOK: HTTP cevabı testte yazılır, DNS çözümü testte verilir.
/// </summary>
public class OfficialSourceInfrastructureTests
{
    private const string Host = "api-sdp.legaseriea.it";
    private const string Url = "https://api-sdp.legaseriea.it/v1/x";

    internal sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; }
            = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Respond(request, ct);
        }
    }

    internal sealed class StubResolver : IOfficialAddressResolver
    {
        public Func<string, IPAddress[]> Map { get; set; } = _ => new[] { IPAddress.Parse("151.101.1.1") };
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(Map(host));
    }

    internal sealed class Env
    {
        private readonly string _name = $"official-{Guid.NewGuid():N}";
        public StubHandler Handler { get; } = new();
        public StubResolver Resolver { get; } = new();
        public OfficialFetcherOptions Options { get; } = new()
        {
            AllowedHostsOverride = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Host, "www.tff.org" }
        };
        public OfficialHostRateLimiter Limiter { get; } = new(TimeSpan.Zero);
        public DateTime Now { get; set; } = new(2026, 9, 11, 17, 45, 0, DateTimeKind.Utc);

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        public OfficialContentFetcher Fetcher(FormaxDbContext db)
            => new(new HttpClient(Handler), new OfficialSourceStore(db), Limiter, Resolver, Options,
                   NullLogger<OfficialContentFetcher>.Instance, () => Now);
    }

    private static OfficialFetchRequest Req(string url = Url, string? round = null)
        => new(OfficialSourceRegistry.SerieASdp, "SerieASdp", url, OfficialPurposes.Lineup, round);

    // ── Güvenlik ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllowlistDisiHost_Reddedilir_AgaCikilmaz()
    {
        var env = new Env();
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req("https://www.flashscore.com/match/abc"));
        Assert.Equal(OfficialFetchOutcomes.HostNotAllowed, r.Outcome);
        Assert.False(r.Ok);
        Assert.Empty(env.Handler.Requests);
        Assert.Equal("HostNotAllowed", db.OfficialSourceFetches.Single().Outcome);
    }

    [Fact]
    public async Task HttpsOlmayanAdres_Reddedilir()
    {
        var env = new Env();
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req("http://api-sdp.legaseriea.it/v1/x"));
        Assert.Equal(OfficialFetchOutcomes.NotHttps, r.Outcome);
        Assert.Empty(env.Handler.Requests);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.10")]
    [InlineData("172.20.0.4")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    public async Task OzelVeYerelIp_Reddedilir(string ip)
    {
        var env = new Env();
        env.Resolver.Map = _ => new[] { IPAddress.Parse(ip) };
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req());
        Assert.Equal(OfficialFetchOutcomes.PrivateAddress, r.Outcome);
        Assert.Empty(env.Handler.Requests);
    }

    [Fact]
    public void AgKorumasi_HerkeseAcikAdresiKabulEder()
    {
        Assert.True(OfficialNetworkGuard.IsPublic(IPAddress.Parse("151.101.1.1")));
        Assert.False(OfficialNetworkGuard.IsPublic(IPAddress.Parse("100.64.0.1")));
        Assert.False(OfficialNetworkGuard.IsPublic(IPAddress.Parse("::ffff:127.0.0.1")));
    }

    [Fact]
    public async Task Yonlendirme_HostYenidenDogrulanir_IzinDisiHedefReddedilir()
    {
        var env = new Env();
        env.Handler.Respond = (_, _) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.Found);
            res.Headers.Location = new Uri("https://evil.example.com/steal");
            return Task.FromResult(res);
        };
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req());
        Assert.Equal(OfficialFetchOutcomes.RedirectRejected, r.Outcome);
        Assert.Single(env.Handler.Requests); // hedefe ASLA gidilmedi
    }

    [Fact]
    public async Task Yonlendirme_IzinliHedefe_IzlenirVeHerAtlamadaDnsDenetlenir()
    {
        var env = new Env();
        var resolved = new List<string>();
        env.Resolver.Map = h => { resolved.Add(h); return new[] { IPAddress.Parse("151.101.1.1") }; };
        env.Handler.Respond = (req, _) =>
        {
            if (req.RequestUri!.Host == Host)
            {
                var res = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                res.Headers.Location = new Uri("https://www.tff.org/final");
                return Task.FromResult(res);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        };
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req());
        Assert.True(r.Ok);
        Assert.Equal(new[] { Host, "www.tff.org" }, resolved);
    }

    [Fact]
    public async Task YanitBoyutuSiniri_Asilirsa_TooLarge()
    {
        var env = new Env();
        env.Options.MaxResponseBytes = 1024;
        env.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 5000))))
        });
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req());
        Assert.Equal(OfficialFetchOutcomes.TooLarge, r.Outcome);
        Assert.Null(r.Body);
        Assert.Empty(db.OfficialSourceCache); // sınır aşan gövde önbelleğe girmez
    }

    [Fact]
    public async Task ZamanAsimi_Timeout()
    {
        var env = new Env();
        env.Options.Timeout = TimeSpan.FromMilliseconds(100);
        env.Handler.Respond = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        using var db = env.NewDb();
        var r = await env.Fetcher(db).FetchAsync(Req());
        Assert.Equal(OfficialFetchOutcomes.Timeout, r.Outcome);
    }

    // ── Ekonomi ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ETagVeLastModified_KosulluGet_304OnbellektenVerilir()
    {
        var env = new Env();
        var calls = 0;
        env.Handler.Respond = (req, _) =>
        {
            calls++;
            if (calls == 1)
            {
                var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"a\":1}") };
                ok.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
                ok.Content.Headers.LastModified = new DateTimeOffset(2026, 9, 11, 17, 0, 0, TimeSpan.Zero);
                return Task.FromResult(ok);
            }
            Assert.Equal("\"v1\"", req.Headers.GetValues("If-None-Match").Single());
            Assert.Contains("2026 17:00:00 GMT", req.Headers.GetValues("If-Modified-Since").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        };

        using (var db = env.NewDb()) Assert.True((await env.Fetcher(db).FetchAsync(Req())).Ok);
        using (var db = env.NewDb())
        {
            var second = await env.Fetcher(db).FetchAsync(Req());
            Assert.Equal(OfficialFetchOutcomes.NotModified, second.Outcome);
            Assert.Equal("{\"a\":1}", second.Body);
            Assert.False(second.ContentChanged);
        }
    }

    [Fact]
    public async Task IcerikHashiDegismediyse_TekrarIslenmez()
    {
        var env = new Env();
        env.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"same\":true}") });

        OfficialFetchResult first;
        using (var db = env.NewDb())
        {
            var f = env.Fetcher(db);
            first = await f.FetchAsync(Req());
            Assert.True(first.ContentChanged);
            Assert.False(first.AlreadyProcessed);
            await f.MarkProcessedAsync(first.Url, first.ContentHash!);
        }
        using (var db = env.NewDb())
        {
            var second = await env.Fetcher(db).FetchAsync(Req());
            Assert.Equal(first.ContentHash, second.ContentHash);
            Assert.False(second.ContentChanged);
            Assert.True(second.AlreadyProcessed);
        }
    }

    [Fact]
    public async Task AyniFeed_BirTurdaBirKezIndirilir()
    {
        var env = new Env();
        using var db = env.NewDb();
        var f = env.Fetcher(db);
        var a = await f.FetchAsync(Req(round: "lineup:202609111745"));
        var b = await f.FetchAsync(Req(round: "lineup:202609111745"));
        var c = await f.FetchAsync(Req(round: "lineup:202609111745"));
        Assert.Single(env.Handler.Requests);
        Assert.Equal(OfficialFetchOutcomes.Fetched, a.Outcome);
        Assert.Equal(OfficialFetchOutcomes.RoundMemo, b.Outcome);
        Assert.Equal(OfficialFetchOutcomes.RoundMemo, c.Outcome);
        Assert.Equal(a.Body, c.Body);
        // Defter her okumayı sayar; tur hafızası "cache hit" olarak işaretlenir.
        Assert.Equal(new[] { false, true, true }, db.OfficialSourceFetches.OrderBy(x => x.Id).Select(x => x.CacheHit).ToArray());
    }

    [Fact]
    public async Task RestartSafeDefter_AyniTurYenidenIndirilmez()
    {
        var env = new Env();
        using (var db = env.NewDb()) await env.Fetcher(db).FetchAsync(Req(round: "lineup:R1"));
        // "Restart": yeni DbContext + yeni fetcher (bellek hafızası boş) — defter ve önbellek kalıcı.
        using (var db = env.NewDb())
        {
            var r = await env.Fetcher(db).FetchAsync(Req(round: "lineup:R1"));
            Assert.Equal(OfficialFetchOutcomes.RoundMemo, r.Outcome);
            Assert.True(r.Ok);
            Assert.Equal(2, db.OfficialSourceFetches.Count());
        }
        Assert.Single(env.Handler.Requests);
        // Yeni tur ise gerçekten sorar.
        using (var db = env.NewDb()) await env.Fetcher(db).FetchAsync(Req(round: "lineup:R2"));
        Assert.Equal(2, env.Handler.Requests.Count);
    }

    [Fact]
    public async Task HostBasinaHizSiniri_ArdisikIsteklerArasindaBekler()
    {
        var limiter = new OfficialHostRateLimiter(TimeSpan.FromMilliseconds(250));
        var now = DateTime.UtcNow;
        using (await limiter.AcquireAsync(Host, () => Task.FromResult<DateTime?>(null), () => DateTime.UtcNow, CancellationToken.None)) { }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (await limiter.AcquireAsync(Host, () => Task.FromResult<DateTime?>(null), () => DateTime.UtcNow, CancellationToken.None)) { }
        Assert.True(sw.ElapsedMilliseconds >= 150, $"bekleme {sw.ElapsedMilliseconds} ms");
    }

    // ── Kayıt defteri ────────────────────────────────────────────────────────

    [Fact]
    public void KayitDefteri_Kilitli11OrganizasyonunHepsiniKapsar_DogrulanmamisaIstekYok()
    {
        foreach (var league in OfficialSourceRegistry.LockedLeagueIds)
            Assert.Contains(OfficialSourceRegistry.All, s => s.LeagueIds.Contains(league));

        // İzin listesi YALNIZ doğrulanmış kaynakların host'ları.
        Assert.Contains("api-sdp.legaseriea.it", OfficialSourceRegistry.AllowedHosts);
        Assert.DoesNotContain("www.efl.com", OfficialSourceRegistry.AllowedHosts);
        Assert.DoesNotContain("www.uefa.com", OfficialSourceRegistry.AllowedHosts);
        Assert.Empty(OfficialSourceRegistry.VerifiedFor(61, OfficialPurposes.Lineup)); // Ligue 1: kaynak yok
        Assert.Equal(OfficialSourceRegistry.SerieASdp,
            OfficialSourceRegistry.VerifiedFor(135, OfficialPurposes.Lineup).First().Key);
        // Resmî olmayan site hiçbir durumda kayıtlı değil.
        Assert.DoesNotContain(OfficialSourceRegistry.All, s => s.Hosts.Any(h => h.Contains("flashscore")));
    }

    // ── Kullanıcı istek yolu dış kaynağa çıkmaz ───────────────────────────────

    [Fact]
    public void KullaniciSayfasi_DisKaynagaCikmaz_BagimliliklardaIndiriciYok()
    {
        var forbidden = new[]
        {
            typeof(IOfficialContentFetcher), typeof(IOfficialCompetitionSource), typeof(HttpClient),
            typeof(IHttpClientFactory), typeof(OfficialLineupCollector)
        };
        var ctorParams = typeof(GetMatchDetailAIContextUseCase).GetConstructors()
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();
        foreach (var t in forbidden) Assert.DoesNotContain(t, ctorParams);
    }
}
