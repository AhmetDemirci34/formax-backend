using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// GÜN-BAZLI TOPLU ÇEKİM — bir günün hatası diğer günlerin verisini DÜŞÜRMEZ.
///
/// GERÇEK API ÇAĞRISI YOK: HttpClient sahte bir handler'a bağlanır, yanıtlar testte
/// yazılır. Çalıştırılan kod üretimdeki gerçek döngüdür.
/// </summary>
public class FixtureDayBatchTests
{
    /// <summary>Gün → gövde eşlemesi veren sahte taşıma katmanı. Ağ trafiği yok.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _byDate;
        public List<string> Requested { get; } = new();

        public StubHandler(Dictionary<string, string> byDate) => _byDate = byDate;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var q = request.RequestUri!.Query;
            var i = q.IndexOf("date=", StringComparison.Ordinal) + 5;
            var date = q.Substring(i, 10);
            Requested.Add(date);

            var body = _byDate.TryGetValue(date, out var b)
                ? b
                : "{\"errors\":{\"x\":\"bilinmeyen gun\"},\"response\":[]}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static string OneFixture(int id, string status, int home, int away, string isoDate) =>
        "{\"errors\":[],\"response\":[{"
        + "\"fixture\":{\"id\":" + id + ",\"date\":\"" + isoDate + "\","
        + "\"status\":{\"short\":\"" + status + "\"},\"venue\":{\"name\":\"Stat\"}},"
        + "\"league\":{\"id\":88,\"name\":\"Eredivisie\",\"round\":\"Regular Season - 3\"},"
        + "\"teams\":{\"home\":{\"id\":1,\"name\":\"Ev\"},\"away\":{\"id\":2,\"name\":\"Dep\"}},"
        + "\"goals\":{\"home\":" + home + ",\"away\":" + away + "},"
        + "\"score\":{\"halftime\":{\"home\":0,\"away\":0}}"
        + "}]}";

    private static string PlanError() =>
        "{\"errors\":{\"plan\":\"Free plans do not have access to this date, try from 2026-08-30 to 2026-09-01.\"},\"response\":[]}";

    private static ApiFootballSportsDataProvider Build(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:ApiKey"] = "test-key",
            ["ApiFootball:BaseUrl"] = "https://stub.local",
            ["ApiFootball:Timezone"] = "UTC"
        }).Build();

        return new ApiFootballSportsDataProvider(
            http,
            new MemoryCache(new MemoryCacheOptions()),
            config,
            NullLogger<ApiFootballSportsDataProvider>.Instance,
            new ApiFootballMetrics());
    }

    [Fact]
    public async Task BirGunHataVerince_BasariliDigerGunlerKorunur()
    {
        var handler = new StubHandler(new Dictionary<string, string>
        {
            ["2026-08-30"] = OneFixture(1001, "FT", 2, 1, "2026-08-30T18:00:00+00:00"),
            // 2026-08-31 eşlemede YOK → gövde hatası döner
            ["2026-09-01"] = OneFixture(1003, "FT", 0, 0, "2026-09-01T18:00:00+00:00")
        });

        var provider = Build(handler);
        var batch = await provider.GetFixturesForDatesAsync(new[]
        {
            new DateTime(2026, 8, 30), new DateTime(2026, 8, 31), new DateTime(2026, 9, 1)
        });

        // Hatalı gün ATILMAZ ama başarılı günlerin verisi DURUR.
        Assert.Equal(3, batch.RequestedDayCount);
        Assert.Equal(2, batch.SucceededDates.Count);
        Assert.Single(batch.FailedDates);
        Assert.Equal(2, batch.Fixtures.Count);
        Assert.False(batch.IsTotalFailure);
        Assert.Contains(batch.Fixtures, f => f.ExternalMatchId == "1001");
        Assert.Contains(batch.Fixtures, f => f.ExternalMatchId == "1003");
    }

    [Fact]
    public async Task HicbirGunAlinamazsa_TotalFailure_Isaretlenir()
    {
        var provider = Build(new StubHandler(new Dictionary<string, string>()));

        var batch = await provider.GetFixturesForDatesAsync(new[]
        {
            new DateTime(2026, 8, 30), new DateTime(2026, 8, 31)
        });

        Assert.True(batch.IsTotalFailure);
        Assert.Empty(batch.Fixtures);
    }

    [Fact]
    public async Task PlanKapaliGun_OgrenilirVeTekrarIstekUretilmez()
    {
        var handler = new StubHandler(new Dictionary<string, string>
        {
            ["2026-08-20"] = PlanError(),
            ["2026-08-30"] = OneFixture(1001, "FT", 1, 0, "2026-08-30T18:00:00+00:00")
        });
        var provider = Build(handler);

        // İlk tur: plan reddi öğrenilir.
        var first = await provider.GetFixturesForDatesAsync(new[]
        {
            new DateTime(2026, 8, 20), new DateTime(2026, 8, 30)
        });
        Assert.Single(first.PlanBlockedDates);

        var afterFirst = handler.Requested.Count;

        // İkinci tur: aynı plan-kapalı gün için İSTEK ÜRETİLMEZ.
        var second = await provider.GetFixturesForDatesAsync(new[] { new DateTime(2026, 8, 20) });

        Assert.Single(second.PlanBlockedDates);
        Assert.Equal(afterFirst, handler.Requested.Count);   // yeni istek YOK
    }

    [Fact]
    public async Task SkorsuzSaglayiciCevabi_FinishedYazdirmaz()
    {
        // Sağlayıcı FT diyor ama goals null → kesin sonuç YOKTUR, 0-0 uydurulmaz.
        const string noScore = """
        {"errors":[],"response":[{
          "fixture":{"id":2001,"date":"2026-08-30T18:00:00+00:00","status":{"short":"FT"}},
          "league":{"id":88,"name":"Eredivisie"},
          "teams":{"home":{"id":1,"name":"Ev"},"away":{"id":2,"name":"Dep"}},
          "goals":{"home":null,"away":null},
          "score":{"halftime":{"home":null,"away":null}}
        }]}
        """;

        var provider = Build(new StubHandler(new Dictionary<string, string>
        {
            ["2026-08-30"] = noScore
        }));

        var batch = await provider.GetFixturesForDatesAsync(new[] { new DateTime(2026, 8, 30) });
        var fixture = Assert.Single(batch.Fixtures);

        Assert.Equal("Finished", fixture.Status);
        Assert.Null(fixture.HomeScore);
        Assert.Null(fixture.AwayScore);   // FixtureSyncJob bu maça skor YAZMAZ
    }

    [Theory]
    [InlineData("FT", "Finished")]
    [InlineData("AET", "Finished")]
    [InlineData("PEN", "Finished")]
    [InlineData("PST", "Postponed")]
    [InlineData("CANC", "Cancelled")]
    [InlineData("ABD", "Cancelled")]
    public async Task SaglayiciDurumKodlari_DogruEslenir(string providerCode, string expected)
    {
        var provider = Build(new StubHandler(new Dictionary<string, string>
        {
            ["2026-08-30"] = OneFixture(3001, providerCode, 1, 1, "2026-08-30T18:00:00+00:00")
        }));

        var batch = await provider.GetFixturesForDatesAsync(new[] { new DateTime(2026, 8, 30) });

        Assert.Equal(expected, Assert.Single(batch.Fixtures).Status);
    }
}
