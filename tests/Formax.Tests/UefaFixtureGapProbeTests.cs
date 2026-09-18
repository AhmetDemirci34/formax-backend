using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Formax.Application.DTOs.Fixtures;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Formax.Tests;

/// <summary>
/// UEFA YAKLAŞAN FİKSTÜR BOŞLUĞU — TEŞHİS ÖLÇÜMÜ. Varsayılan koşuda ATLANIR.
///
/// Yalnız <c>FORMAX_LIVE_APIFOOTBALL=1</c> iken çalışır ve ÜRETİM sağlayıcısını (aynı istemci, aynı
/// önbellek, aynı sayaç) kullanarak takım başına TEK <c>fixtures?team={id}&amp;next=N</c> isteği yapar.
/// Amaç: FORMAX'ta 0 yaklaşan UCL/UECL maçı olmasının nedeni api-football'un bu fikstürleri hiç
/// yayımlamaması mı, yoksa FORMAX hattında kaybolması mı — bunu KANITLAMAK.
///
/// Kota: takım başına 1 istek. Anahtar ortam değişkeninden okunur ve HİÇBİR yere yazılmaz.
/// </summary>
public class UefaFixtureGapProbeTests
{
    private readonly ITestOutputHelper _out;
    public UefaFixtureGapProbeTests(ITestOutputHelper output) => _out = output;

    private static bool Enabled => Environment.GetEnvironmentVariable("FORMAX_LIVE_APIFOOTBALL") == "1";

    /// <summary>Ölçülecek takımlar: UEFA müsabakalarında oynayan, kapsam içi kulüpler.</summary>
    public static IEnumerable<object[]> Teams() => new[]
    {
        new object[] { "157", "Bayern München", "UCL (10.09 lig aşaması), Timeline watermark 19.08" },
        new object[] { "194", "Ajax", "UECL (27.08 play-off), Timeline watermark 15.09" }
    };

    [SkippableTheory]
    [MemberData(nameof(Teams))]
    public async Task TakimYaklasanFiksturleri_LigDagilimi(string externalTeamId, string name, string note)
    {
        Skip.IfNot(Enabled, "FORMAX_LIVE_APIFOOTBALL=1 değil");

        // Anahtar ortam değişkeninden (ApiFootball__ApiKey) alınır; değeri hiçbir yere yazılmaz.
        var key = Environment.GetEnvironmentVariable("ApiFootball__ApiKey");
        Assert.False(string.IsNullOrWhiteSpace(key), "ApiFootball__ApiKey ortam değişkeni yok");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:ApiKey"] = key,
            ["Timeline:NextN"] = "20"
        }).Build();

        using var http = new HttpClient();
        var provider = new ApiFootballSportsDataProvider(
            http, new MemoryCache(new MemoryCacheOptions()), config,
            NullLogger<ApiFootballSportsDataProvider>.Instance, new ApiFootballMetrics());

        var future = await provider.GetTeamUpcomingFixturesAsync(externalTeamId);

        _out.WriteLine($"=== {name} (team={externalTeamId}) — {note}");
        _out.WriteLine($"yaklaşan fikstür sayısı: {future.Count}");
        foreach (var g in future.GroupBy(f => (f.LeagueExternalId, f.LeagueName)).OrderBy(g => g.Key.LeagueExternalId))
            _out.WriteLine($"   lig {g.Key.LeagueExternalId,-5} {g.Key.LeagueName,-32} {g.Count()} maç " +
                           $"(ilk {g.Min(x => x.MatchDate):yyyy-MM-dd}, son {g.Max(x => x.MatchDate):yyyy-MM-dd})");
        foreach (var f in future.OrderBy(f => f.MatchDate).Take(20))
            _out.WriteLine($"   {f.MatchDate:yyyy-MM-dd HH:mm}Z  lig {f.LeagueExternalId,-5} " +
                           $"{f.HomeTeamName} – {f.AwayTeamName}  [{f.Status}] ext={f.ExternalMatchId}");

        var uefa = future.Where(f => f.LeagueExternalId is 2 or 3 or 848).ToList();
        _out.WriteLine($"   → UEFA (2/3/848) fikstürü: {uefa.Count}");
        Assert.NotEmpty(future);
    }
}
