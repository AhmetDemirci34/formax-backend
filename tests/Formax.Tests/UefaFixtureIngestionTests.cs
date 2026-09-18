using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Http;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// UEFA YAKLAŞAN FİKSTÜR ALIMI (18.09.2026) — Şampiyonlar Ligi (2) ve Konferans Ligi (848).
///
/// ÖLÇÜLMÜŞ KÖK NEDEN: api-football FREE planı ileri takvim keşfinin BÜTÜN kanallarını kapatıyor:
///   fixtures?team=&amp;next=N     → "Free plans do not have access to the Next parameter."
///   fixtures?team=&amp;last=N     → "Free plans do not have access to the Last parameter."
///   fixtures?league=2&amp;season=2026 → "Free plans do not have access to this season, try from 2022 to 2024."
///   fixtures?date=YYYY-MM-DD   → yalnız [bugün−1, bugün+1]
/// Tek çalışan ileri kanal <c>fixtures?date=bugün+1</c>'dir; UEFA maçları da bu pencereye girdiğinde
/// (T−1) alınır — ölçüldü: 16-17.09 Avrupa Ligi'nin 18 maçı bu yolla yazıldı.
///
/// FORMAX tarafındaki TEK gerçek kusur: takım penceresi ayağı plan reddini ALGILAMIYOR, boş cevabı
/// "bu takımın maçı yok" sayıp 6 saat önbelleğe alıyor ve işi "kapsam-dışı" damgalıyordu (sahte başarı
/// + sessiz kota kaçağı). Bu dosya o düzeltmeyi ve UEFA alım hattını sabitler. GERÇEK AĞ YOK.
/// </summary>
public class UefaFixtureIngestionTests : IDisposable
{
    private const int Ucl = LockedCompetitions.ChampionsLeague;     // 2
    private const int Uecl = LockedCompetitions.ConferenceLeague;   // 848

    private readonly FormaxDbContext _db = new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase($"uefa-fx-{Guid.NewGuid():N}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    public void Dispose() => _db.Dispose();

    // ── Sağlayıcı katmanı: sahte taşıma ───────────────────────────────────────

    /// <summary>İstenen adresi kaydeden, gövdeyi testten alan sahte taşıma katmanı.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<string, string> _body;
        public List<string> Requested { get; } = new();

        public StubHandler(Func<string, string> body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.PathAndQuery;
            Requested.Add(url);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body(url), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static (ApiFootballSportsDataProvider provider, StubHandler handler, ApiFootballPlanState plan)
        BuildProvider(Func<string, string> body)
    {
        var handler = new StubHandler(body);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:ApiKey"] = "test-key",
            ["ApiFootball:BaseUrl"] = "https://stub.local",
            ["ApiFootball:Timezone"] = "UTC",
            ["Timeline:NextN"] = "20",
            ["Timeline:LastN"] = "20"
        }).Build();
        var plan = new ApiFootballPlanState();
        var provider = new ApiFootballSportsDataProvider(
            new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()), config,
            NullLogger<ApiFootballSportsDataProvider>.Instance, new ApiFootballMetrics(), plan);
        return (provider, handler, plan);
    }

    /// <summary>GERÇEK plan ret gövdeleri (18.09.2026 ölçümü).</summary>
    private const string NextPlanError =
        "{\"errors\":{\"plan\":\"Free plans do not have access to the Next parameter.\"},\"response\":[]}";
    private const string LastPlanError =
        "{\"errors\":{\"plan\":\"Free plans do not have access to the Last parameter.\"},\"response\":[]}";
    private const string SeasonPlanError =
        "{\"errors\":{\"plan\":\"Free plans do not have access to this season, try from 2022 to 2024.\"},\"response\":[]}";
    private const string DatePlanError =
        "{\"errors\":{\"plan\":\"Free plans do not have access to this date, try from 2026-09-17 to 2026-09-19.\"},\"response\":[]}";

    private static string Fixture(int id, int leagueId, string leagueName, string status, string isoDate,
        int homeTeam = 157, string homeName = "Bayern München", int awayTeam = 33, string awayName = "Manchester United",
        int? homeGoals = null, int? awayGoals = null) =>
        "{\"fixture\":{\"id\":" + id + ",\"date\":\"" + isoDate + "\",\"status\":{\"short\":\"" + status + "\"},"
        + "\"venue\":{\"name\":\"Allianz Arena\"}},"
        + "\"league\":{\"id\":" + leagueId + ",\"name\":\"" + leagueName + "\",\"round\":\"League phase - 2\"},"
        + "\"teams\":{\"home\":{\"id\":" + homeTeam + ",\"name\":\"" + homeName + "\"},"
        + "\"away\":{\"id\":" + awayTeam + ",\"name\":\"" + awayName + "\"}},"
        + "\"goals\":{\"home\":" + (homeGoals?.ToString() ?? "null") + ",\"away\":" + (awayGoals?.ToString() ?? "null") + "},"
        + "\"score\":{\"halftime\":{\"home\":null,\"away\":null}}}";

    private static string Ok(params string[] fixtures)
        => "{\"errors\":[],\"response\":[" + string.Join(",", fixtures) + "]}";

    // ── 12. Plan engelinde sahte başarı yazılmaz (ASIL DÜZELTME) ─────────────

    [Theory]
    [InlineData(true, LastPlanError)]
    [InlineData(false, NextPlanError)]
    public async Task TakimPenceresi_PlanReddi_BosCevapOnbelleklenmez_IkinciIstekUretilmez(bool past, string body)
    {
        var (provider, handler, plan) = BuildProvider(_ => body);

        var first = past
            ? await provider.GetTeamRecentResultsAsync("157")
            : await provider.GetTeamUpcomingFixturesAsync("157");

        Assert.Empty(first);                       // sahte fikstür ÜRETİLMEZ
        Assert.True(plan.TeamWindowBlocked);       // ret ÖĞRENİLDİ
        Assert.Single(handler.Requested);

        // İkinci çağrı: plan reddi hatırlandığı için HİÇ istek üretilmez (kota kaçağı kapandı).
        var second = past
            ? await provider.GetTeamRecentResultsAsync("157")
            : await provider.GetTeamUpcomingFixturesAsync("157");
        Assert.Empty(second);
        Assert.Single(handler.Requested);

        // Diğer ayak da aynı reddi paylaşır: iki uç da kapalıdır.
        var other = past
            ? await provider.GetTeamUpcomingFixturesAsync("157")
            : await provider.GetTeamRecentResultsAsync("157");
        Assert.Empty(other);
        Assert.Single(handler.Requested);
    }

    [Fact]
    public async Task TakimPenceresi_PlanDisiGeciciHata_Onbelleklenmez_SonrakiTurYenidenDener()
    {
        var calls = 0;
        var (provider, handler, plan) = BuildProvider(_ =>
            ++calls == 1
                ? "{\"errors\":{\"requests\":\"Too many requests\"},\"response\":[]}"
                : Ok(Fixture(9001, Ucl, "UEFA Champions League", "NS", "2026-09-30T19:00:00+00:00")));

        Assert.Empty(await provider.GetTeamUpcomingFixturesAsync("157"));
        Assert.False(plan.TeamWindowBlocked);      // GEÇİCİ hata plan reddi SAYILMAZ

        // Boş cevap önbelleğe alınmadığı için sonraki tur yeniden dener ve veriyi alır.
        var retry = await provider.GetTeamUpcomingFixturesAsync("157");
        Assert.Equal(Ucl, Assert.Single(retry).LeagueExternalId);
        Assert.Equal(2, handler.Requested.Count);
    }

    // ── 11. Cache hit → gerçek HTTP çıkmaz ──────────────────────────────────

    [Fact]
    public async Task TakimPenceresi_BasariliCevap_Onbelleklenir_IkinciIstekUretmez()
    {
        var (provider, handler, _) = BuildProvider(_ =>
            Ok(Fixture(9002, Ucl, "UEFA Champions League", "NS", "2026-09-30T19:00:00+00:00")));

        Assert.Single(await provider.GetTeamUpcomingFixturesAsync("157"));
        Assert.Single(await provider.GetTeamUpcomingFixturesAsync("157"));
        Assert.Single(handler.Requested);
    }

    [Fact]
    public void PlanReddiAyirdEdici_YalnizTakimPenceresiniTanir()
    {
        Assert.True(ApiFootballPlanState.IsTeamWindowRestriction(NextPlanError));
        Assert.True(ApiFootballPlanState.IsTeamWindowRestriction(LastPlanError));
        // Tarih ve sezon kısıtları BAŞKA mekanizmalarla ele alınır; takım penceresi kapatılmaz.
        Assert.False(ApiFootballPlanState.IsTeamWindowRestriction(DatePlanError));
        Assert.False(ApiFootballPlanState.IsTeamWindowRestriction(SeasonPlanError));
        Assert.False(ApiFootballPlanState.IsTeamWindowRestriction(null));
        Assert.False(ApiFootballPlanState.IsTeamWindowRestriction(""));

        var plan = new ApiFootballPlanState();
        Assert.True(plan.BlockTeamWindow(NextPlanError, new DateTime(2026, 9, 18, 7, 0, 0, DateTimeKind.Utc)));
        Assert.False(plan.BlockTeamWindow(NextPlanError, DateTime.UtcNow));   // ikinci kez uyarı yazılmaz
        Assert.True(plan.TeamWindowBlocked);
        Assert.Equal(new DateTime(2026, 9, 18, 7, 0, 0, DateTimeKind.Utc), plan.TeamWindowBlockedAtUtc);
    }

    // ── 1 + 2 + 3 + 9. Lig/sezon eşlemesi, yaklaşan durum, UTC dönüşümü ─────

    [Theory]
    [InlineData(Ucl, "UEFA Champions League")]
    [InlineData(Uecl, "UEFA Europa Conference League")]
    public async Task GunBazliCekim_UefaFiksturunuEsler_YaklasanDurumVeUtcKorunur(int leagueId, string leagueName)
    {
        var (provider, _, _) = BuildProvider(_ =>
            Ok(Fixture(9100 + leagueId, leagueId, leagueName, "NS", "2026-09-30T21:00:00+02:00")));

        var batch = await provider.GetFixturesForDatesAsync(new[] { new DateTime(2026, 9, 30) });
        var fixture = Assert.Single(batch.Fixtures);

        // FORMAX organizasyon kimliği = api-football lig kimliği (ayrı eşleme tablosu YOK).
        Assert.Equal(leagueId, fixture.LeagueExternalId);
        Assert.True(LockedCompetitions.IsLocked(fixture.LeagueExternalId));
        Assert.Contains(leagueId, LockedCompetitions.Uefa);
        Assert.Equal("NotStarted", fixture.Status);
        Assert.Null(fixture.HomeScore);
        // +02:00 yerel → 19:00Z (saat dilimi kayması yok).
        Assert.Equal(new DateTime(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc), fixture.MatchDate);
        Assert.Equal(DateTimeKind.Utc, fixture.MatchDate.Kind);
    }

    [Fact]
    public void KilitliKapsam_UclVeUecl_AllowListtenGecer()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            LockedCompetitions.All.Select((id, i) => new KeyValuePair<string, string?>(
                $"Coverage:LeagueAllowList:{i}", id.ToString())).ToList()).Build();
        var allow = CoveragePolicy.LeagueAllowList(config);

        Assert.Equal(11, allow.Count);
        Assert.True(CoveragePolicy.Allows(allow, Ucl));
        Assert.True(CoveragePolicy.Allows(allow, Uecl));
        Assert.True(CoveragePolicy.Allows(allow, LockedCompetitions.EuropaLeague));
        Assert.False(CoveragePolicy.Allows(allow, 667));   // kapsam dışı (Friendlies Clubs)
    }

    // ── Alım hattı: gerçek depo + gerçek tur ────────────────────────────────

    private sealed class DayProvider : StubSportsDataProvider
    {
        private readonly List<SportsFixtureResult> _fixtures;
        public int Calls;
        public DayProvider(params SportsFixtureResult[] fixtures) => _fixtures = fixtures.ToList();

        public override Task<SportsFixtureDayBatch> GetFixturesForDatesAsync(
            IReadOnlyList<DateTime> dates, CancellationToken ct = default)
        {
            Calls++;
            var batch = new SportsFixtureDayBatch { RequestedDayCount = dates.Count };
            foreach (var d in dates) batch.SucceededDates.Add(d);
            batch.Fixtures.AddRange(_fixtures);
            return Task.FromResult(batch);
        }
    }

    private sealed class FakeLock : IFixtureSyncLockRepository
    {
        public Task<bool> TryAcquireAsync(string id, TimeSpan s, CancellationToken ct = default) => Task.FromResult(true);
        public Task HeartbeatAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStats : IMatchLiveStatsRepository
    {
        public MatchLiveStats? GetByMatchId(int matchId) => null;
        public List<MatchLiveStats> GetByMatchIds(IEnumerable<int> matchIds) => new();
        public Task UpsertAsync(MatchLiveStats s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAsync(MatchLiveStats s, MatchLiveStats? e, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopStandings : ILeagueStandingsService
    {
        public Task RefreshForSettledMatchAsync(int l, DateTime d, CancellationToken ct = default) => Task.CompletedTask;
        public LeagueStandingsSnapshotDto? GetCached(int leagueId, int seasonId) => null;
        public Task<LeagueStandingsSnapshotDto?> GetAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<LeagueStandingsSnapshotDto?> RefreshAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<int> RefreshCurrentSeasonAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private static SportsFixtureResult Incoming(
        string extId, int leagueId, string leagueName, DateTime kickoffUtc, string status = "NotStarted",
        string homeExt = "157", string homeName = "Bayern München",
        string awayExt = "33", string awayName = "Manchester United") => new()
    {
        ExternalMatchId = extId,
        MatchDate = kickoffUtc,
        Status = status,
        LeagueName = leagueName,
        LeagueExternalId = leagueId,
        HomeTeamExternalId = homeExt, HomeTeamName = homeName,
        AwayTeamExternalId = awayExt, AwayTeamName = awayName
    };

    private (FixtureSyncJob job, DayProvider provider) BuildJob(params SportsFixtureResult[] fixtures)
    {
        var provider = new DayProvider(fixtures);
        var settings = new Dictionary<string, string?>
        {
            ["ApiFootball:Timezone"] = "Europe/Istanbul",
            ["ApiFootball:Results:Enabled"] = "false",
            ["ApiFootball:ResultReconciliation:MaxDaysPerCycle"] = "0",
            ["ApiFootball:ResultReconciliation:MaxFixtureLookupsPerCycle"] = "0",
            ["ApiFootball:FutureFixtureRefinement:MaxSingleFixtureRequestsPerUtcDay"] = "0"
        };
        for (var i = 0; i < LockedCompetitions.All.Count; i++)
            settings[$"Coverage:LeagueAllowList:{i}"] = LockedCompetitions.All[i].ToString();

        var services = new ServiceCollection();
        services.AddSingleton<IFixtureSyncLockRepository>(new FakeLock());
        services.AddSingleton<ISportsDataProvider>(provider);
        services.AddSingleton<IFixtureSyncRepository>(new FixtureSyncRepository(_db));
        services.AddSingleton<IMatchLiveStatsRepository>(new FakeStats());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton(new ApiFootballMetrics());
        services.AddSingleton<ILeagueStandingsService>(new NoopStandings());

        var sp = services.BuildServiceProvider();
        return (new FixtureSyncJob(sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FixtureSyncJob>.Instance), provider);
    }

    private static DateTime Future(int days, int hourUtc = 19)
        => DateTime.UtcNow.Date.AddDays(days).AddHours(hourUtc);

    private Task RunCycle(FixtureSyncJob job)
        => job.RunBackfillAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1));

    // ── 1 + 2 + 3 + 8. UCL/UECL fikstürü DB'ye yazılır ──────────────────────

    [Fact]
    public async Task UclVeUeclFiksturleri_DbyeYazilir_TakimlarOlusturulur_SkorUydurulmaz()
    {
        var uclKickoff = Future(2);
        var ueclKickoff = Future(3, 16);
        var (job, _) = BuildJob(
            Incoming("1700001", Ucl, "UEFA Champions League", uclKickoff),
            Incoming("1700002", Uecl, "UEFA Europa Conference League", ueclKickoff,
                homeExt: "194", homeName: "Ajax", awayExt: "546", awayName: "Getafe"));

        await RunCycle(job);

        var ucl = _db.Matches.Single(m => m.ExternalMatchId == "1700001");
        Assert.Equal(Ucl, ucl.LeagueId);
        Assert.Equal(uclKickoff, ucl.MatchDate);
        Assert.Equal(MatchStatuses.NotStarted, ucl.Status);
        Assert.Equal(0, ucl.HomeScore);            // yaklaşan maça skor UYDURULMAZ
        Assert.Equal(0, ucl.AwayScore);

        var uecl = _db.Matches.Single(m => m.ExternalMatchId == "1700002");
        Assert.Equal(Uecl, uecl.LeagueId);
        Assert.Equal(ueclKickoff, uecl.MatchDate);

        // Takımlar sağlayıcı kimliğiyle oluşturuldu (sahte takım yok).
        Assert.Equal(4, _db.Teams.Count());
        Assert.Contains(_db.Teams, t => t.ExternalTeamId == "157" && t.Name == "Bayern München");
        Assert.Contains(_db.Teams, t => t.ExternalTeamId == "194" && t.Name == "Ajax");
    }

    // ── UEFA ÖNCE, API-FOOTBALL SONRA: kanonik maç devralınır, DUPLICATE AÇILMAZ ──

    [Fact]
    public async Task ResmiUefaFiksturu_TminusBirdeApiFootballdanGelince_DuplicateAcilmaz_KimlikDevralinir()
    {
        var kickoff = Future(2);
        // Resmî UEFA kaynağının yazdığı satır: api-football kimliği YOK, takvim kaynağı resmî.
        _db.Teams.AddRange(
            new Team { Id = 700, Name = "Bayern München", ExternalTeamId = "157" },
            new Team { Id = 701, Name = "Manchester United", ExternalTeamId = "33" });
        _db.Matches.Add(new Match
        {
            Id = 970001, ExternalMatchId = null, LeagueId = Ucl, League = "UEFA Champions League",
            MatchDate = kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 700, AwayTeamId = 701,
            ScheduleSource = "official:uefa-match-api", KickoffPrecision = KickoffPrecisions.Confirmed
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        // api-football aynı maçı T−1'de kendi kimliğiyle getiriyor (saat 10 dk kaymış).
        var (job, _) = BuildJob(Incoming("1700100", Ucl, "UEFA Champions League", kickoff.AddMinutes(10)));
        await RunCycle(job);

        var rows = _db.Matches.Where(m => m.LeagueId == Ucl).ToList();
        Assert.Single(rows);                                   // DUPLICATE YOK
        Assert.Equal(970001, rows[0].Id);                      // kanonik kimlik korundu
        Assert.Equal("1700100", rows[0].ExternalMatchId);      // ikinci sağlayıcı referansı devralındı
        Assert.Equal((700, 701), (rows[0].HomeTeamId, rows[0].AwayTeamId));
        // RESMÎ SAAT GERİ ALINMAZ: ScheduleSource "official:" olduğu için sağlayıcı saati yazılmadı.
        Assert.Equal(kickoff, rows[0].MatchDate);
    }

    [Fact]
    public async Task KanonikAdayBelirsizse_ApiFootballTuru_DuplicateAcmaz_MacYazmaz()
    {
        var kickoff = Future(2);
        _db.Teams.AddRange(
            new Team { Id = 710, Name = "Bayern München", ExternalTeamId = "157" },
            new Team { Id = 711, Name = "Manchester United", ExternalTeamId = "33" });
        // Aynı sıralı çift, aynı pencerede İKİ kanonik satır → belirsiz.
        _db.Matches.AddRange(
            new Match { Id = 970010, LeagueId = Ucl, MatchDate = kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 710, AwayTeamId = 711 },
            new Match { Id = 970011, LeagueId = Ucl, MatchDate = kickoff.AddHours(2), Status = MatchStatuses.NotStarted, HomeTeamId = 710, AwayTeamId = 711 });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var (job, _) = BuildJob(Incoming("1700200", Ucl, "UEFA Champions League", kickoff));
        await RunCycle(job);

        Assert.Equal(2, _db.Matches.Count(m => m.LeagueId == Ucl));      // üçüncü satır AÇILMADI
        Assert.DoesNotContain(_db.Matches.ToList(), m => m.ExternalMatchId == "1700200");
    }

    // ── 6. Idempotent upsert ────────────────────────────────────────────────

    [Fact]
    public async Task AyniExternalFixtureId_IkinciTurda_DuplicateYaratmaz_KimlikDegismez()
    {
        var kickoff = Future(2);
        var (job, _) = BuildJob(Incoming("1700003", Ucl, "UEFA Champions League", kickoff));

        await RunCycle(job);
        var firstId = _db.Matches.Single(m => m.ExternalMatchId == "1700003").Id;
        _db.ChangeTracker.Clear();

        await RunCycle(job);

        var rows = _db.Matches.Where(m => m.ExternalMatchId == "1700003").ToList();
        Assert.Single(rows);
        Assert.Equal(firstId, rows[0].Id);
        Assert.Equal(kickoff, rows[0].MatchDate);
    }

    // ── 5. Ertelenen / yeni tarihe alınan fikstür güncellenir ───────────────

    [Fact]
    public async Task ErtelenenFikstur_YeniTarih_MevcutKaydaYazilir_DuplicateYok()
    {
        var original = Future(2);
        var moved = Future(9, 16);
        var (job, _) = BuildJob(Incoming("1700004", Ucl, "UEFA Champions League", original));
        await RunCycle(job);
        _db.ChangeTracker.Clear();

        var (moveJob, _) = BuildJob(Incoming("1700004", Ucl, "UEFA Champions League", moved));
        await RunCycle(moveJob);

        var row = Assert.Single(_db.Matches.Where(m => m.ExternalMatchId == "1700004").ToList());
        Assert.Equal(moved, row.MatchDate);
        Assert.Equal(MatchStatuses.NotStarted, row.Status);
    }

    // ── 7. Takım eşleşmesi çözülemezse maç yazılmaz ─────────────────────────

    [Fact]
    public async Task TakimKimligiYoksa_MacYazilmaz_SahteTakimUretilmez()
    {
        var (job, _) = BuildJob(
            Incoming("1700005", Ucl, "UEFA Champions League", Future(2), homeExt: "", awayExt: ""));

        await RunCycle(job);

        Assert.Empty(_db.Matches.Where(m => m.ExternalMatchId == "1700005").ToList());
        Assert.Empty(_db.Teams.ToList());
    }

    // ── 13. Sağlayıcı sıfır döndürürse sahte fikstür oluşmaz ────────────────

    [Fact]
    public async Task SaglayiciSifirFiksturDonerse_HicbirMacYazilmaz()
    {
        var (job, provider) = BuildJob();

        await RunCycle(job);

        Assert.Equal(1, provider.Calls);
        Assert.Empty(_db.Matches.ToList());
        Assert.Empty(_db.Teams.ToList());
    }

    // ── 15. Kapsam içi diğer ligler etkilenmez ──────────────────────────────

    [Fact]
    public async Task UefaFiksturuYazilirken_DigerDokuzOrganizasyonunKaydiBozulmaz()
    {
        var existing = Future(1, 15);
        _db.Teams.AddRange(
            new Team { Id = 900, Name = "Liverpool", ExternalTeamId = "40" },
            new Team { Id = 901, Name = "Fulham", ExternalTeamId = "36" });
        _db.Matches.Add(new Match
        {
            Id = 990001, ExternalMatchId = "1600001", LeagueId = LockedCompetitions.PremierLeague,
            League = "Premier League", MatchDate = existing, Status = MatchStatuses.NotStarted,
            HomeTeamId = 900, AwayTeamId = 901
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var (job, _) = BuildJob(Incoming("1700006", Ucl, "UEFA Champions League", Future(2)));
        await RunCycle(job);

        var pl = _db.Matches.Single(m => m.ExternalMatchId == "1600001");
        Assert.Equal(existing, pl.MatchDate);
        Assert.Equal(MatchStatuses.NotStarted, pl.Status);
        Assert.Equal(LockedCompetitions.PremierLeague, pl.LeagueId);
        Assert.Equal(2, _db.Matches.Count());
    }

    // ── 4 + 14. Yaklaşan maç listesi filtresi ───────────────────────────────

    [Fact]
    public async Task YaklasanListe_UclMacini_Gosterir_EskiBitmisMaciGostermez()
    {
        _db.Teams.AddRange(
            new Team { Id = 910, Name = "Bayern München", ExternalTeamId = "157" },
            new Team { Id = 911, Name = "Manchester United", ExternalTeamId = "33" });
        _db.Matches.AddRange(
            new Match
            {
                Id = 991001, ExternalMatchId = "1700010", LeagueId = Ucl, League = "UEFA Champions League",
                MatchDate = DateTime.UtcNow.AddDays(2), Status = MatchStatuses.NotStarted,
                HomeTeamId = 910, AwayTeamId = 911
            },
            // 9 gün önce bitmiş maç: "şimdi" merkezli pencerenin DIŞINDA.
            new Match
            {
                Id = 991002, ExternalMatchId = "1700011", LeagueId = Ucl, League = "UEFA Champions League",
                MatchDate = DateTime.UtcNow.AddDays(-9), Status = MatchStatuses.Finished,
                HomeTeamId = 911, AwayTeamId = 910, HomeScore = 1, AwayScore = 1
            });
        _db.SaveChanges();

        var config = new ConfigurationBuilder().Build();
        var list = await new MatchReadRepository(_db, config).GetScreenMatchListAsync();

        Assert.Contains(list, m => m.MatchId == 991001 && m.Status == "Scheduled");
        Assert.DoesNotContain(list, m => m.MatchId == 991002);
    }
}
