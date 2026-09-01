using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Http;
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KOTA KAPISI VE CACHE — düşük öncelikli iş bloklanır, kritik sonuç işi eldeki
/// veriyi kullanmaya devam eder. Gerçek sağlayıcıya istek YOK.
/// </summary>
public class BudgetAndCacheTests
{
    private const int DailyLimit = 100;
    private const int Reserve = 20;

    [Fact]
    public void ButceDolunca_DusukOncelikliIs_Bloklanir()
    {
        // Kritik olmayan çağıranın tavanı limit - rezerv = 80.
        var ceiling = ApiFootballCacheHandler.CeilingFor("OddsIngestionJob", DailyLimit, Reserve);

        Assert.Equal(80, ceiling);
        Assert.True(85 >= ceiling);   // 85 istek harcanmışken oran işi BLOKLANIR
    }

    [Fact]
    public void KritikSonucIsi_RezervDiliminiKullanabilir()
    {
        var critical = ApiFootballCacheHandler.CeilingFor("FixtureSyncJob", DailyLimit, Reserve);
        var normal = ApiFootballCacheHandler.CeilingFor("OddsIngestionJob", DailyLimit, Reserve);

        Assert.Equal(DailyLimit, critical);
        Assert.True(critical > normal);
        Assert.False(85 >= critical);  // 85 harcanmışken sonuç işi hâlâ geçebilir
    }

    [Fact]
    public void EtiketsizCagiran_RezerviKullanamaz()
    {
        Assert.Equal(80, ApiFootballCacheHandler.CeilingFor(null, DailyLimit, Reserve));
        Assert.Equal(80, ApiFootballCacheHandler.CeilingFor(
            ApiFootballCallScope.Unattributed, DailyLimit, Reserve));
    }

    /// <summary>Çağrıldığı an testi düşüren handler — "istek YAPILMAMALI" kanıtı.</summary>
    private sealed class ExplodingHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errors\":[],\"response\":[]}")
            });
        }
    }

    [Fact]
    public async Task KritikIs_MevcutCacheVerisiniKullanir_YeniIstekUretmeden()
    {
        var l1 = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:DailyRequestLimit"] = DailyLimit.ToString(),
            ["ApiFootball:CriticalReserve"] = Reserve.ToString()
            // ConnectionStrings:FormaxDB YOK → L2 deposu yapılandırılmamış, DB'ye gidilmez.
        }).Build();

        var inner = new ExplodingHandler();
        var metrics = new ApiFootballMetrics();
        var handler = new ApiFootballCacheHandler(
            l1,
            new ApiFootballHttpCacheStore(string.Empty, NullLogger<ApiFootballHttpCacheStore>.Instance),
            metrics, config, NullLogger<ApiFootballCacheHandler>.Instance)
        { InnerHandler = inner };

        var uri = new Uri("https://stub.local/fixtures?date=2026-08-30");

        // Cache'e önceden konmuş yanıt (önceki turun aldığı veri).
        var key = ApiFootballEndpointFamily.Classify(uri) + "|" + ApiFootballCacheHandler.NormalizeKey(uri);
        var cachedBody = "{\"errors\":[],\"response\":[{\"fixture\":{\"id\":9001}}]}";
        l1.Set(key, new ApiFootballCacheHandler.CachedResponse(HttpStatusCode.OK, cachedBody));

        using var client = new HttpClient(handler);
        using var _scope = ApiFootballCallScope.Begin("FixtureSyncJob");
        var response = await client.GetAsync(uri);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(cachedBody, body);
        Assert.Equal(0, inner.Calls);                       // GERÇEK İSTEK ÜRETİLMEDİ
        Assert.Equal(1, metrics.Snapshot().CacheHits);
    }

    [Fact]
    public void GovdeHatasiOlanYanit_CachelenMEZ()
    {
        var uri = new Uri("https://stub.local/fixtures?date=2026-08-20");
        var planError =
            "{\"errors\":{\"plan\":\"Free plans do not have access to this date.\"},\"response\":[]}";

        var (cacheable, _, _) = ApiFootballCacheHandler.Evaluate("fixtures", uri, planError);

        Assert.False(cacheable);
    }
}
