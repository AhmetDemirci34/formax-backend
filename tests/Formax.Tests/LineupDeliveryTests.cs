using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Lineup;
using Formax.Application.Services.Matches;
using Formax.Application.UseCases;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Lineups;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KADRONUN MAÇTAN ÖNCE GÜVENİLİR TESLİMİ.
///
/// ÖLÇÜLEN HATA (11.09.2026, Venezia–Fiorentina, MatchId 15383 / fikstür 1550126):
/// T−28,5'te sağlayıcı boş döndü. Boş cevap 20 dk L2 + 15 dk bellek negatif cache'ine
/// girdi, fikstür başına 20 dk soğuma vardı, döngü 5 dakikaydı ve pencere T−5'te kesin
/// kapanıyordu. Sonuç: T−10 slotu soğumaya, T−5 slotu kapanan pencereye takıldı; FORMAX
/// son 28 dakikada sağlayıcıya hiç sormadı.
///
/// Gerçek ağ YOK: stub sağlayıcı, HttpMessageHandler stub'ı ve EF Core InMemory.
/// </summary>
public class LineupDeliveryTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 11, 18, 45, 0, DateTimeKind.Utc);
    private static DateTime T(double minutesFromKickoff) => Kickoff.AddMinutes(minutesFromKickoff);

    private const int MatchId = 15383;
    private const string Fixture = "1550126";
    private const int HomeExt = 517;   // Venezia
    private const int AwayExt = 502;   // Fiorentina

    // ═══ 1. SLOTLAR ════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(-91, null)]
    [InlineData(-90, 0)]
    [InlineData(-61, 0)]
    [InlineData(-60, 1)]
    [InlineData(-31, 1)]
    [InlineData(-30, 2)]
    [InlineData(-16, 2)]
    [InlineData(-15, 3)]
    [InlineData(-11, 3)]
    [InlineData(-10, 4)]
    [InlineData(-6, 4)]
    [InlineData(-5, 5)]
    [InlineData(0, 5)]
    [InlineData(9, 5)]      // kaçırılan T−5 kickoff+10'a kadar yakalanır
    [InlineData(11, null)]  // sonra yoklama biter
    public void Slotlar_T90_T60_T30_T15_T10_T5(double minute, int? expected)
        => Assert.Equal(expected, LineupPollSchedule.DueSlot(Kickoff, T(minute)));

    [Fact]
    public void SlotSabitleri()
    {
        Assert.Equal(new[] { 90, 60, 30, 15, 10, 5 }, LineupPollSchedule.SlotMinutesBeforeKickoff);
        Assert.Equal(TimeSpan.FromMinutes(90), LineupPollSchedule.WindowOpen);
        Assert.Equal(TimeSpan.FromMinutes(10), LineupPollSchedule.CatchUpGrace);
        Assert.True(LineupIngestionService.PerFixtureCooldown < TimeSpan.FromMinutes(5));
    }

    // ═══ 2. KAÇIRILAN SLOT SONRAKİ TURDA ÇALIŞIR ══════════════════════════════

    [Fact]
    public void KacirilanSlot_SonrakiTurdaCalisir_BirikmezTekIstek()
    {
        // Son gerçek kontrol T−31 (T−60 slotu). İş T−30 ve T−15 anlarında çalışmadı.
        var last = T(-31);
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, T(-12), false, last));   // T−15 yakalanır

        // T−12'de yapılan tek istek T−30 ve T−15'i birlikte kapatır: aynı slotta ikinci yok.
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, T(-11), false, T(-12)));
        // T−10 geldiğinde yeni slot.
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, T(-10), false, T(-12)));
    }

    [Fact]
    public void VeneziaFiorentinaSenaryosu_T10veT5ArtikKacmaz()
    {
        // Gerçek defter: son gerçek kontrol 18:16:27 (T−28,5). Eski kod 18:35 ve 18:40
        // turlarında istek atmadı. Yeni kural:
        var last = new DateTime(2026, 9, 11, 18, 16, 27, DateTimeKind.Utc);
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, new DateTime(2026, 9, 11, 18, 35, 21, DateTimeKind.Utc), false, last));
        Assert.True(LineupPollSchedule.ShouldPoll(Kickoff, new DateTime(2026, 9, 11, 18, 40, 21, DateTimeKind.Utc), false,
            new DateTime(2026, 9, 11, 18, 35, 21, DateTimeKind.Utc)));
    }

    // ═══ 3. AYNI SLOTTA DUPLICATE İSTEK YOK ═══════════════════════════════════

    [Fact]
    public void AyniSlot_DuplicateIstekYok()
    {
        var last = T(-15);
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, T(-14), false, last));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, T(-11), false, last));
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, T(-80), false, T(-85)));
    }

    [Fact]
    public async Task AyniSlot_IkiTur_TekSaglayiciIstegi()
    {
        using var env = new Env();
        env.Provider.Next = () => null;   // geçerli boş cevap

        await env.RunAsync(T(-15));
        await env.RunAsync(T(-14));
        await env.RunAsync(T(-11));

        Assert.Equal(1, env.Provider.Calls);
    }

    // ═══ 4. BOŞ CEVAP BAŞARI DEĞİL VE SONRAKİ SLOTU ENGELLEMEZ ══════════════

    [Fact]
    public async Task BosCevap_BasariSayilmaz_SonrakiSlotuEngellemez()
    {
        using var env = new Env();
        env.Provider.Next = () => null;

        var first = await env.RunAsync(T(-15));
        Assert.Equal(LineupFetchOutcome.Empty, Assert.Single(first).Result.Outcome);

        using (var db = env.NewDb())
        {
            var header = db.MatchLineups.Single(l => l.MatchId == MatchId);
            Assert.False(header.HomeLineupsReleased);
            Assert.False(header.AwayLineupsReleased);
            Assert.Equal(T(-15), header.LastCheckedAtUtc);
            Assert.Empty(db.MatchLineupPlayers.Where(p => p.MatchId == MatchId));
            Assert.Equal("NoData", db.FixtureRefreshAttempts.Single().LastOutcome);
        }

        // T−10: yeni slot → sağlayıcıya YENİDEN gidilir (boş cevap engel olmadı).
        env.Provider.Next = VeneziaFiorentina;
        var second = await env.RunAsync(T(-10));
        Assert.Equal(LineupFetchOutcome.Released, Assert.Single(second).Result.Outcome);
        Assert.Equal(2, env.Provider.Calls);
    }

    // ═══ 5. BAŞARILI KADRO TEKRAR İSTENMEZ ════════════════════════════════════

    [Fact]
    public async Task BasariliKadro_TekrarIstenmez()
    {
        using var env = new Env();
        env.Provider.Next = VeneziaFiorentina;

        await env.RunAsync(T(-60));
        await env.RunAsync(T(-30));
        await env.RunAsync(T(-15));
        await env.RunAsync(T(-5));

        Assert.Equal(1, env.Provider.Calls);
        Assert.False(LineupPollSchedule.ShouldPoll(Kickoff, T(-5), lineupAlreadyComplete: true, lastRealCheckUtc: null));
    }

    // ═══ 6. RESTART LEDGER'I SIFIRLAMAZ ═══════════════════════════════════════

    [Fact]
    public async Task Restart_SonKontrolKorunur()
    {
        using var env = new Env();
        env.Provider.Next = () => null;
        await env.RunAsync(T(-30));

        // "Restart": yeni DbContext + yeni servis + yeni sağlayıcı sayacı; aynı kalıcı depo.
        var restarted = new CountingProvider { Next = () => null };
        using var db = env.NewDb();
        var service = Env.Service(db, restarted);
        var match = db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Single();
        var results = await service.RunSlotsAsync(new[] { match }, T(-25), 30);

        Assert.Empty(results);
        Assert.Equal(0, restarted.Calls);
    }

    // ═══ 7. PLAN / RATE LIMIT / BÜTÇE ENGELİ BAŞARI SAYILMAZ ══════════════════

    [Fact]
    public async Task PlanEngeli_BasariVeKontrolSayilmaz_SlotAcikKalir()
    {
        using var env = new Env();
        env.Provider.Throw = () => new ApiFootballSportsDataProvider.ApiFootballUnavailableException("plan: Free plans do not have access");

        var r = await env.RunAsync(T(-15));
        Assert.Equal(LineupFetchOutcome.ProviderError, Assert.Single(r).Result.Outcome);

        using (var db = env.NewDb())
        {
            Assert.Null(db.MatchLineups.SingleOrDefault(l => l.MatchId == MatchId)?.LastCheckedAtUtc);
            Assert.Empty(db.MatchLineupPlayers);
            Assert.Equal("ProviderError", db.FixtureRefreshAttempts.Single().LastOutcome);
        }

        // Soğuma (2 dk) sonrası AYNI slotta yeniden denenir; hata slotu harcamadı.
        env.Provider.Throw = null;
        env.Provider.Next = VeneziaFiorentina;
        var retry = await env.RunAsync(T(-12));
        Assert.Equal(LineupFetchOutcome.Released, Assert.Single(retry).Result.Outcome);
    }

    [Fact]
    public async Task ButceEngeli_DenemeSayilmaz()
    {
        using var env = new Env();
        env.Provider.Throw = () => new ApiFootballSportsDataProvider.ApiFootballBudgetBlockedException("formax_budget");

        var r = await env.RunAsync(T(-15));
        Assert.Equal(LineupFetchOutcome.BudgetBlocked, Assert.Single(r).Result.Outcome);

        using var db = env.NewDb();
        var row = db.FixtureRefreshAttempts.Single();
        Assert.Equal(0, row.AttemptCount);                  // günlük tavana sayılmadı
        Assert.Equal("Blocked:Budget", row.LastOutcome);
        Assert.Null(db.MatchLineups.SingleOrDefault()?.LastCheckedAtUtc);
    }

    // ═══ 8. DOĞRU UÇ + CEVAP SINIFLANDIRMASI (gerçek sağlayıcı, stub HTTP) ════

    [Fact]
    public async Task DogruUc_FixturesLineups_VeBosCevapCachelenmez()
    {
        var handler = new StubHandler("{\"errors\":[],\"results\":0,\"response\":[]}");
        var provider = BuildProvider(handler);

        Assert.Null(await provider.GetOfficialLineupAsync(Fixture));
        Assert.Null(await provider.GetOfficialLineupAsync(Fixture));

        Assert.Equal(2, handler.Requests.Count);                           // negatif cache YOK
        Assert.All(handler.Requests, u =>
        {
            Assert.Equal("/fixtures/lineups", u.AbsolutePath);
            Assert.Equal("?fixture=1550126", u.Query);
        });
    }

    [Fact]
    public async Task SaglayiciHatasi_BosSayilmaz_IstisnaAtar()
    {
        var plan = BuildProvider(new StubHandler("{\"errors\":{\"plan\":\"Free plans do not have access\"},\"response\":[]}"));
        await Assert.ThrowsAsync<ApiFootballSportsDataProvider.ApiFootballUnavailableException>(
            () => plan.GetOfficialLineupAsync(Fixture));

        var budget = BuildProvider(new StubHandler("{\"errors\":{\"formax_budget\":\"daily provider budget exhausted\"},\"results\":0,\"response\":[]}"));
        await Assert.ThrowsAsync<ApiFootballSportsDataProvider.ApiFootballBudgetBlockedException>(
            () => budget.GetOfficialLineupAsync(Fixture));
    }

    [Fact]
    public async Task Saglayici_TakimKimligiVeTeknikDirektoruTasir()
    {
        var provider = BuildProvider(new StubHandler(LineupJson(firstTeam: AwayExt, secondTeam: HomeExt)));
        var r = await provider.GetOfficialLineupAsync(Fixture);

        Assert.NotNull(r);
        Assert.Equal(AwayExt, r!.HomeTeamExternalId);   // sağlayıcı sırası olduğu gibi
        Assert.Equal(HomeExt, r.AwayTeamExternalId);
        Assert.Equal("S. Pioli", r.HomeCoach);
        Assert.Equal("4-3-3", r.HomeFormation);
        Assert.Equal(11, r.HomeStarters.Count);
    }

    [Fact]
    public void BosCevap_L2CachedeTutulmaz_KadroYediGun()
    {
        var uri = new Uri("https://stub.local/fixtures/lineups?fixture=1550126");
        var empty = Formax.Infrastructure.Http.ApiFootballCacheHandler.Evaluate(
            "fixtures/lineups", uri, "{\"errors\":[],\"response\":[]}");
        Assert.False(empty.cacheable);

        var full = Formax.Infrastructure.Http.ApiFootballCacheHandler.Evaluate(
            "fixtures/lineups", uri, LineupJson(HomeExt, AwayExt));
        Assert.True(full.cacheable);
        Assert.Equal(TimeSpan.FromDays(7), full.ttl);
    }

    // ═══ 9. İLK 11 / YEDEK / FORMASYON EŞLEMESİ ═══════════════════════════════

    [Fact]
    public async Task IlkOnbirYedekFormasyon_TakimKimligiyleDogruTarafa()
    {
        using var env = new Env();
        // Sağlayıcı TERS sırada veriyor: ilk satır Fiorentina.
        env.Provider.Next = () => Lineup(firstTeam: AwayExt, secondTeam: HomeExt,
            firstFormation: "4-3-3", secondFormation: "3-5-2", firstCoach: "S. Pioli", secondCoach: "G. Stroppa");

        var r = Assert.Single(await env.RunAsync(T(-10)));
        Assert.Equal(LineupNormalizer.Orientation.SwappedByTeamId, r.Result.Orientation);

        using var db = env.NewDb();
        var header = db.MatchLineups.Single();
        Assert.Equal("3-5-2", header.HomeFormation);   // Venezia
        Assert.Equal("4-3-3", header.AwayFormation);   // Fiorentina
        Assert.Equal(HomeExt, header.HomeTeamExternalId);
        Assert.Equal(AwayExt, header.AwayTeamExternalId);
        Assert.Equal("G. Stroppa", header.HomeCoach);
        Assert.Equal("S. Pioli", header.AwayCoach);
        Assert.Equal(Fixture, header.ExternalFixtureId);
        Assert.Equal("api-football", header.Provider);
        Assert.Equal(T(-10), header.FetchedAt);
        Assert.Equal(T(-10), header.LastCheckedAtUtc);
        Assert.True(header.HomeLineupsReleased && header.AwayLineupsReleased);

        var players = db.MatchLineupPlayers.ToList();
        Assert.Equal(11, players.Count(p => p.Side == "Home" && p.Role == "Starter"));
        Assert.Equal(11, players.Count(p => p.Side == "Away" && p.Role == "Starter"));
        Assert.Equal(9, players.Count(p => p.Side == "Home" && p.Role == "Bench"));
        Assert.Equal(9, players.Count(p => p.Side == "Away" && p.Role == "Bench"));
        // Venezia oyuncuları ev sahibi, Fiorentina oyuncuları deplasman tarafında.
        Assert.All(players.Where(p => p.Side == "Home"), p => Assert.StartsWith($"P{HomeExt}-", p.PlayerName));
        Assert.All(players.Where(p => p.Side == "Away"), p => Assert.StartsWith($"P{AwayExt}-", p.PlayerName));
    }

    // ═══ 10. DUPLICATE OYUNCU YAZILMAZ ═════════════════════════════════════════

    [Fact]
    public async Task DuplicateOyuncu_Yazilmaz()
    {
        using var env = new Env();
        env.Provider.Next = () =>
        {
            var l = Lineup(HomeExt, AwayExt);
            l.HomeStarters.Add(l.HomeStarters[0]);                          // aynı oyuncu iki kez
            l.HomeBench.Add(new SportsLineupPlayer { Name = l.HomeStarters[1].Name, ShirtNumber = l.HomeStarters[1].ShirtNumber });
            return l;
        };

        await env.RunAsync(T(-10));

        using var db = env.NewDb();
        var home = db.MatchLineupPlayers.Where(p => p.Side == "Home").ToList();
        Assert.Equal(20, home.Count);
        Assert.Equal(home.Count, home.Select(p => p.ShirtNumber + "|" + p.PlayerName).Distinct().Count());
        Assert.Equal(11, home.Count(p => p.Role == "Starter"));
    }

    [Fact]
    public async Task IkinciYazim_OyunculariCogaltmaz()
    {
        using var env = new Env();
        using (var db = env.NewDb())
        {
            var service = Env.Service(db, new CountingProvider { Next = VeneziaFiorentina });
            var match = db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Single();
            await service.FetchAndStoreAsync(match, T(-10));
            await service.FetchAndStoreAsync(match, T(-9));
        }
        using var check = env.NewDb();
        Assert.Equal(40, check.MatchLineupPlayers.Count());
    }

    // ═══ 11. KADRO YAZILINCA DETAY OKUMASI HEMEN GÖRÜR ════════════════════════

    /// <summary>
    /// Sunucu tarafında maç-detay DTO önbelleği YOKTUR: detay kadroyu her istekte DB'den
    /// kurar. Yazımdan sonraki ilk okuma (yeni bağlam) yeni kadroyu ve son kontrolü görür;
    /// "kadro yok" yanıtını tutan bir ara katman kalmaz.
    /// </summary>
    [Fact]
    public async Task KadroYazilinca_SonrakiDetayOkumasiHemenGorur_OnbellekYok()
    {
        using var env = new Env();
        env.Provider.Next = () => null;
        await env.RunAsync(T(-15));

        using (var before = env.NewDb())
        {
            var repo = new MatchLineupRepository(before);
            Assert.False(repo.GetByMatchId(MatchId)!.HomeLineupsReleased);
            Assert.Equal(LineupAvailability.SourceDelayed, LineupAvailability.Resolve(false, Kickoff, T(-14)));
        }

        env.Provider.Next = VeneziaFiorentina;
        await env.RunAsync(T(-10));

        using var after = env.NewDb();
        var fresh = new MatchLineupRepository(after);
        Assert.True(fresh.GetByMatchId(MatchId)!.HomeLineupsReleased);
        Assert.Equal(T(-10), fresh.GetByMatchId(MatchId)!.LastCheckedAtUtc);
        Assert.Equal(40, fresh.GetPlayersByMatchId(MatchId).Count);

        // Detay use case'i ne bellek önbelleği ne de sağlayıcı taşır.
        var ctorParams = typeof(GetMatchDetailAIContextUseCase).GetConstructors()
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();
        Assert.DoesNotContain(typeof(IMemoryCache), ctorParams);
        Assert.DoesNotContain(typeof(Formax.Application.Interfaces.ISportsDataProvider), ctorParams);
        Assert.DoesNotContain(typeof(LineupIngestionService), ctorParams);
    }

    // ═══ 12. BAŞLAMIŞ MAÇTA DB'DEKİ KADRO GÖSTERİLİR ═══════════════════════════

    [Fact]
    public void BaslamisMacta_DbdekiKadro_Gosterilir_YoksaDurumDogru()
    {
        Assert.Equal(LineupAvailability.Released, LineupAvailability.Resolve(true, Kickoff, T(60)));
        Assert.Equal(LineupAvailability.Released, LineupAvailability.Resolve(true, Kickoff, T(60 * 24)));
        Assert.Equal(LineupAvailability.SourceDelayed, LineupAvailability.Resolve(false, Kickoff, T(-13)));
        Assert.Equal(LineupAvailability.SourceDelayed, LineupAvailability.Resolve(false, Kickoff, T(8)));
        Assert.Equal(LineupAvailability.NotFound, LineupAvailability.Resolve(false, Kickoff, T(30)));
        Assert.Equal(LineupAvailability.Waiting, LineupAvailability.Resolve(false, Kickoff, T(-120)));
    }

    // ═══ 12b. "SON KONTROL" YALNIZ GERÇEK KONTROLDÜR ══════════════════════════

    [Fact]
    public void SonKontrol_EngellenenRezervasyonuGostermez()
    {
        var real = T(-28.5);
        var blocked = T(-4);

        // Başlıkta gerçek kontrol varsa o kullanılır (sonraki engeller onu değiştirmez).
        Assert.Equal(real, LineupAvailability.LastRealCheck(real, real, blocked, "Blocked:Budget"));
        // Eski kayıt (başlık yok): defter yalnız gerçek cevapla kapandıysa.
        Assert.Equal(real, LineupAvailability.LastRealCheck(null, null, real, "NoData"));
        Assert.Equal(real, LineupAvailability.LastRealCheck(null, null, real, "Applied"));
        Assert.Null(LineupAvailability.LastRealCheck(null, null, blocked, "Blocked:Budget"));
        Assert.Null(LineupAvailability.LastRealCheck(null, null, blocked, "ProviderError"));
        Assert.Null(LineupAvailability.LastRealCheck(null, null, blocked, "Reserved"));
    }

    // ═══ 13. SAYFA AÇILIŞI SAĞLAYICI İSTEĞİ ÜRETMEZ ═══════════════════════════

    [Fact]
    public void SayfaAcilisi_SaglayiciIstegiUretmez()
    {
        // Maç-detay yolu yalnız depo okur: kadro alım servisine de sağlayıcıya da bağımlı değil
        // (bkz. yukarıdaki kurucu kontrolü). Okuma deposu yazma/istek metodu çağırmadan döner.
        using var env = new Env();
        using var db = env.NewDb();
        var repo = new MatchLineupRepository(db);
        Assert.Null(repo.GetByMatchId(MatchId));
        Assert.Empty(repo.GetPlayersByMatchId(MatchId));
        Assert.Equal(0, env.Provider.Calls);
    }

    // ── Yardımcılar ────────────────────────────────────────────────────────────

    private static SportsLineupResult VeneziaFiorentina()
        => Lineup(HomeExt, AwayExt, "3-5-2", "4-3-3", "G. Stroppa", "S. Pioli");

    private static SportsLineupResult Lineup(int firstTeam, int secondTeam,
        string firstFormation = "3-5-2", string secondFormation = "4-3-3",
        string? firstCoach = null, string? secondCoach = null)
    {
        static List<SportsLineupPlayer> Players(int team, int from, int count) =>
            Enumerable.Range(from, count).Select(i => new SportsLineupPlayer
            {
                Name = $"P{team}-{i}", ShirtNumber = i, Position = i == 1 ? "G" : "M",
                Grid = from == 1 ? $"{(i == 1 ? 1 : 2)}:{i}" : null
            }).ToList();

        return new SportsLineupResult
        {
            LineupsAnnounced = true,
            HomeFormation = firstFormation,
            AwayFormation = secondFormation,
            HomeStarters = Players(firstTeam, 1, 11),
            HomeBench = Players(firstTeam, 12, 9),
            AwayStarters = Players(secondTeam, 1, 11),
            AwayBench = Players(secondTeam, 12, 9),
            HomeTeamExternalId = firstTeam,
            AwayTeamExternalId = secondTeam,
            HomeCoach = firstCoach,
            AwayCoach = secondCoach
        };
    }

    private static string LineupJson(int firstTeam, int secondTeam)
    {
        static string Team(int id, string formation, string coach)
        {
            var xi = string.Join(",", Enumerable.Range(1, 11).Select(i =>
                $"{{\"player\":{{\"name\":\"P{id}-{i}\",\"number\":{i},\"pos\":\"M\",\"grid\":\"2:{i}\"}}}}"));
            var subs = string.Join(",", Enumerable.Range(12, 9).Select(i =>
                $"{{\"player\":{{\"name\":\"P{id}-{i}\",\"number\":{i},\"pos\":\"M\",\"grid\":null}}}}"));
            return $"{{\"team\":{{\"id\":{id},\"name\":\"T{id}\"}},\"coach\":{{\"id\":1,\"name\":\"{coach}\"}}," +
                   $"\"formation\":\"{formation}\",\"startXI\":[{xi}],\"substitutes\":[{subs}]}}";
        }
        return "{\"errors\":[],\"results\":2,\"response\":[" +
               Team(firstTeam, "4-3-3", "S. Pioli") + "," + Team(secondTeam, "3-5-2", "G. Stroppa") + "]}";
    }

    private static ApiFootballSportsDataProvider BuildProvider(StubHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:ApiKey"] = "test-key",
            ["ApiFootball:BaseUrl"] = "https://stub.local",
            ["ApiFootball:Timezone"] = "UTC"
        }).Build();
        return new ApiFootballSportsDataProvider(new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()), config,
            NullLogger<ApiFootballSportsDataProvider>.Instance, new ApiFootballMetrics());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public List<Uri> Requests { get; } = new();
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingProvider : StubSportsDataProvider
    {
        public int Calls;
        public Func<SportsLineupResult?>? Next;
        public Func<Exception>? Throw;
        public readonly List<string> Fixtures = new();

        public override Task<SportsLineupResult?> GetOfficialLineupAsync(string matchExternalId, CancellationToken ct = default)
        {
            Calls++;
            Fixtures.Add(matchExternalId);
            if (Throw != null) throw Throw();
            return Task.FromResult(Next?.Invoke());
        }
    }

    /// <summary>Bellek içi kalıcı depo — aynı ad = aynı veritabanı (restart benzetimi).</summary>
    private sealed class Env : IDisposable
    {
        private readonly string _name = $"lineup-{Guid.NewGuid():N}";
        public CountingProvider Provider { get; } = new();

        public Env()
        {
            using var db = NewDb();
            db.Teams.Add(new Team { Id = 1, Name = "Venezia", ExternalTeamId = HomeExt.ToString() });
            db.Teams.Add(new Team { Id = 2, Name = "Fiorentina", ExternalTeamId = AwayExt.ToString() });
            db.Matches.Add(new Match
            {
                Id = MatchId, ExternalMatchId = Fixture, LeagueId = 135, League = "Serie A",
                MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2
            });
            db.SaveChanges();
        }

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        public static LineupIngestionService Service(FormaxDbContext db, CountingProvider provider)
            => new(new MatchLineupRepository(db), new FixtureSyncRepository(db), provider,
                   NullLogger<LineupIngestionService>.Instance);

        public async Task<IReadOnlyList<(Match Match, LineupFetchResult Result)>> RunAsync(DateTime nowUtc)
        {
            using var db = NewDb();
            var match = db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).AsNoTracking().Single();
            return await Service(db, Provider).RunSlotsAsync(new[] { match }, nowUtc, dailyCap: 30);
        }

        public void Dispose() { }
    }
}
