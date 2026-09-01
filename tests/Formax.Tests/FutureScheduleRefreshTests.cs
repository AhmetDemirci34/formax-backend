using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Standings;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// GELECEK FİKSTÜR TAKVİM DOĞRULAMA — geçici kickoff'un gerçek saate dönmesi.
/// GERÇEK SAĞLAYICI ÇAĞRISI YOK: fake provider kullanılır ve çağrı sayısı sayılır.
/// </summary>
public class FutureScheduleRefreshTests : IDisposable
{
    private const int SuperLig = LockedCompetitions.SuperLig;
    private const string ExtId = "1584394";                 // gerçek ExternalMatchId
    private static readonly DateTime NowUtc = new(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<FormaxDbContext> _options;
    private readonly FormaxDbContext _db;

    public FutureScheduleRefreshTests()
    {
        _options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"future-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(_options);
    }

    // ── Sahte sağlayıcı: çağrı sayar, istenen yanıtı döner ────────────────────
    private sealed class CountingProvider : StubSportsDataProvider
    {
        private readonly Dictionary<string, SportsFixtureResult?> _byId;
        public int Calls { get; private set; }

        public CountingProvider(Dictionary<string, SportsFixtureResult?> byId) => _byId = byId;

        public override Task<SportsFixtureResult?> GetFixtureByIdAsync(string id, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_byId.TryGetValue(id, out var r) ? r : null);
        }

        public override Task<SportsFixtureDayBatch> GetFixturesForDatesAsync(
            IReadOnlyList<DateTime> dates, CancellationToken ct = default)
        {
            var b = new SportsFixtureDayBatch { RequestedDayCount = dates.Count };
            foreach (var d in dates) b.SucceededDates.Add(d.Date);
            return Task.FromResult(b);   // gün-bazlı çekim bu testlerin konusu değil
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
        public MatchLiveStats? GetByMatchId(int id) => null;
        public List<MatchLiveStats> GetByMatchIds(IEnumerable<int> ids) => new();
        public Task UpsertAsync(MatchLiveStats s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAsync(MatchLiveStats s, MatchLiveStats? e, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopStandings : ILeagueStandingsService
    {
        public Task RefreshForSettledMatchAsync(int l, DateTime d, CancellationToken ct = default) => Task.CompletedTask;
        public LeagueStandingsSnapshotDto? GetCached(int l, int s) => null;
        public Task<LeagueStandingsSnapshotDto?> GetAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<LeagueStandingsSnapshotDto?> RefreshAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<int> RefreshCurrentSeasonAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    // ── Kurulum ───────────────────────────────────────────────────────────────

    private void SeedMatch(DateTime kickoffUtc, string precision, string status = "NotStarted")
    {
        _db.Teams.Add(new Team { Id = 3661, Name = "Başakşehir", ExternalTeamId = "564" });
        _db.Teams.Add(new Team { Id = 3309, Name = "Galatasaray", ExternalTeamId = "645" });
        _db.Matches.Add(new Match
        {
            Id = 98933, ExternalMatchId = ExtId, LeagueId = SuperLig, League = "Süper Lig",
            HomeTeamId = 3661, AwayTeamId = 3309,
            MatchDate = kickoffUtc, Status = status, KickoffPrecision = precision
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private static SportsFixtureResult ProviderFixture(
        DateTime kickoffUtc, int leagueId = SuperLig, bool provisional = false) => new()
    {
        ExternalMatchId = ExtId,
        MatchDate = kickoffUtc,
        Status = "NotStarted",
        LeagueName = "Süper Lig",
        LeagueExternalId = leagueId,
        HomeTeamExternalId = "564", HomeTeamName = "Başakşehir",
        AwayTeamExternalId = "645", AwayTeamName = "Galatasaray",
        KickoffProvisional = provisional
    };

    private (FixtureSyncJob job, CountingProvider provider) BuildJob(
        Dictionary<string, SportsFixtureResult?> responses, int futureCap = 5)
    {
        var provider = new CountingProvider(responses);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:Timezone"] = "Europe/Istanbul",
            ["Coverage:LeagueAllowList:0"] = SuperLig.ToString(),
            ["ApiFootball:ResultReconciliation:MaxDaysPerCycle"] = "0",
            ["ApiFootball:ResultReconciliation:MaxFixtureLookupsPerCycle"] = "0",
            ["ApiFootball:FutureFixtureRefinement:MaxSingleFixtureRequestsPerUtcDay"] = futureCap.ToString(),
            ["ApiFootball:FutureFixtureRefinement:PerFixtureRetryCooldownHours"] = "24",
            ["ApiFootball:FutureFixtureRefinement:FixtureLookupSpacingMs"] = "0"
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IFixtureSyncLockRepository>(new FakeLock());
        services.AddSingleton<ISportsDataProvider>(provider);
        services.AddSingleton<IFixtureSyncRepository>(new FixtureSyncRepository(_db));
        services.AddSingleton<IMatchLiveStatsRepository>(new FakeStats());
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(new ApiFootballMetrics());
        services.AddSingleton<ILeagueStandingsService>(new NoopStandings());

        var sp = services.BuildServiceProvider();
        return (new FixtureSyncJob(sp.GetRequiredService<IServiceScopeFactory>(),
                                   NullLogger<FixtureSyncJob>.Instance), provider);
    }

    private Task RunCycle(FixtureSyncJob job)
        => job.RunBackfillAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 1));

    // ── Testler ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BasaksehirGalatasaray_KabulSenaryosu()
    {
        // BAŞLANGIÇ: geçici tarih 6 Eylül 12:00 — UI'ın 4 günlük penceresine girmiyor.
        SeedMatch(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc), KickoffPrecisions.Provisional);

        // SAĞLAYICI: gerçek tarih 4 Eylül 17:00 UTC (TR 20:00), kesin.
        var (job, provider) = BuildJob(new Dictionary<string, SportsFixtureResult?>
        {
            [ExtId] = ProviderFixture(new DateTime(2026, 9, 4, 17, 0, 0, DateTimeKind.Utc))
        });

        await RunCycle(job);
        _db.ChangeTracker.Clear();

        var rows = _db.Matches.AsNoTracking().Where(m => m.ExternalMatchId == ExtId).ToList();
        var m = Assert.Single(rows);                          // DUPLICATE OLUŞMADI

        Assert.Equal(98933, m.Id);                            // aynı satır güncellendi
        Assert.Equal(new DateTime(2026, 9, 4, 17, 0, 0), m.MatchDate);
        Assert.Equal(KickoffPrecisions.Confirmed, m.KickoffPrecision);
        Assert.NotNull(m.ScheduleVerifiedAtUtc);
        Assert.Equal(SuperLig, m.LeagueId);

        // Türkiye gösterimi 4 Eylül 20:00
        var tz = Formax.Infrastructure.Http.ApiFootballTimeZone.TryResolve("Europe/Istanbul")!;
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc), tz);
        Assert.Equal(new DateTime(2026, 9, 4, 20, 0, 0), local);

        // Dört takvim günü penceresine GİRER (1 Eylül'den bakıldığında)
        var windowEnd = MatchReadRepository.FourCalendarDayWindowEndUtc(NowUtc);
        Assert.True(m.MatchDate <= windowEnd);

        Assert.Equal(1, provider.Calls);                      // tek tekil çağrı
    }

    [Fact]
    public async Task GecersizSaglayiciCevabi_MevcutKaydiBOZMAZ()
    {
        var original = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        SeedMatch(original, KickoffPrecisions.Provisional);

        // Sağlayıcı BAŞKA bir ligin fikstürünü döndürdü — kimlik uyuşmuyor.
        var (job, _) = BuildJob(new Dictionary<string, SportsFixtureResult?>
        {
            [ExtId] = ProviderFixture(new DateTime(2026, 9, 4, 17, 0, 0, DateTimeKind.Utc), leagueId: 39)
        });

        await RunCycle(job);
        _db.ChangeTracker.Clear();

        var m = _db.Matches.AsNoTracking().Single(x => x.ExternalMatchId == ExtId);
        Assert.Equal(original, m.MatchDate);                  // KORUNDU
        Assert.Equal(KickoffPrecisions.Provisional, m.KickoffPrecision);
    }

    [Fact]
    public async Task SaglayiciBosDonerse_MevcutKaydiBOZMAZ()
    {
        var original = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        SeedMatch(original, KickoffPrecisions.Provisional);

        var (job, provider) = BuildJob(new Dictionary<string, SportsFixtureResult?> { [ExtId] = null });

        await RunCycle(job);
        _db.ChangeTracker.Clear();

        var m = _db.Matches.AsNoTracking().Single(x => x.ExternalMatchId == ExtId);
        Assert.Equal(original, m.MatchDate);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ConfirmedFikstur_TTLIcinde_YenidenIstenmez()
    {
        SeedMatch(new DateTime(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc), KickoffPrecisions.Confirmed);

        var (job, provider) = BuildJob(new Dictionary<string, SportsFixtureResult?>
        {
            [ExtId] = ProviderFixture(new DateTime(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc))
        });

        await RunCycle(job);

        Assert.Equal(0, provider.Calls);   // kesinleşmiş kickoff bütçe harcamaz
    }

    [Fact]
    public async Task Restart_AyniFikstruYenidenISTEMEZ()
    {
        SeedMatch(new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc), KickoffPrecisions.Provisional);

        var (job1, p1) = BuildJob(new Dictionary<string, SportsFixtureResult?> { [ExtId] = null });
        await RunCycle(job1);
        Assert.Equal(1, p1.Calls);

        // SÜREÇ YENİDEN BAŞLADI — defter kalıcı, soğuma 24 saat.
        var (job2, p2) = BuildJob(new Dictionary<string, SportsFixtureResult?> { [ExtId] = null });
        await RunCycle(job2);

        Assert.Equal(0, p2.Calls);
    }

    [Fact]
    public async Task GunlukTavan_AsilamAZ()
    {
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });
        for (var i = 0; i < 12; i++)
            _db.Matches.Add(new Match
            {
                Id = 5000 + i, ExternalMatchId = $"ext{i}", LeagueId = SuperLig,
                HomeTeamId = 1, AwayTeamId = 2,
                MatchDate = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                Status = "NotStarted", KickoffPrecision = KickoffPrecisions.Provisional
            });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var (job, provider) = BuildJob(new Dictionary<string, SportsFixtureResult?>(), futureCap: 3);
        await RunCycle(job);

        Assert.Equal(3, provider.Calls);   // 12 aday, tavan 3
    }

    [Fact]
    public async Task EnYakinTarih_OncelikliDir()
    {
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });
        // Uzak maç önce eklenir; öncelik yine de en yakına gitmeli.
        _db.Matches.Add(new Match
        {
            Id = 6001, ExternalMatchId = "uzak", LeagueId = SuperLig, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
            Status = "NotStarted", KickoffPrecision = KickoffPrecisions.Provisional
        });
        _db.Matches.Add(new Match
        {
            Id = 6002, ExternalMatchId = "yakin", LeagueId = SuperLig, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc),
            Status = "NotStarted", KickoffPrecision = KickoffPrecisions.Provisional
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var (job, _) = BuildJob(new Dictionary<string, SportsFixtureResult?>(), futureCap: 1);
        await RunCycle(job);
        _db.ChangeTracker.Clear();

        var attempted = _db.FixtureRefreshAttempts.AsNoTracking()
            .Where(a => a.Purpose == FixtureRefreshPurposes.FutureSchedule)
            .Select(a => a.ExternalMatchId).ToList();

        Assert.Equal(new[] { "yakin" }, attempted);
    }

    [Fact]
    public void AyniNominalTarihliTurda_SiralamaDETERMINISTIKtir()
    {
        // GERÇEK DURUM (ölçüldü 01.09.2026): yer-tutucu tarih bir TURUN TAMAMINA aynı
        // değeri verir — 9 Süper Lig adayının hepsi "2026-09-06 12:00:00". Yalnız
        // MatchDate'e göre sıralamak, günlük 5'lik bütçeyi hangi maçların alacağını
        // veritabanının keyfine bırakırdı.
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });

        var sameKickoff = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        // Bilerek KARIŞIK sırada eklenir.
        foreach (var id in new[] { 105135, 42846, 103471, 98933, 99157, 103399 })
            _db.Matches.Add(new Match
            {
                Id = id, ExternalMatchId = $"ext{id}", LeagueId = SuperLig,
                HomeTeamId = 1, AwayTeamId = 2, MatchDate = sameKickoff,
                Status = "NotStarted", KickoffPrecision = KickoffPrecisions.Provisional
            });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var repo = new FixtureSyncRepository(_db);
        var run1 = repo.GetFutureScheduleRefreshCandidates(
            NowUtc, NowUtc.AddDays(8), LockedCompetitions.All.ToList()).Select(m => m.Id).ToList();
        var run2 = new FixtureSyncRepository(new FormaxDbContext(_options))
            .GetFutureScheduleRefreshCandidates(
                NowUtc, NowUtc.AddDays(8), LockedCompetitions.All.ToList()).Select(m => m.Id).ToList();

        Assert.Equal(run1, run2);                                   // iki tur AYNI sıra
        Assert.Equal(new[] { 42846, 98933, 99157, 103399, 103471, 105135 }, run1);
        Assert.Equal(2, run1.IndexOf(98933) + 1);                   // 98933 ilk 5 içinde
    }

    [Fact]
    public void KapsamDisiLig_AdayOlamaz()
    {
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });
        _db.Matches.Add(new Match
        {
            Id = 7777, ExternalMatchId = "disari", LeagueId = 667,   // kapsam dışı
            HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc),
            Status = "NotStarted", KickoffPrecision = KickoffPrecisions.Provisional
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var candidates = new FixtureSyncRepository(_db).GetFutureScheduleRefreshCandidates(
            NowUtc, NowUtc.AddDays(8), LockedCompetitions.All.ToList());

        Assert.Empty(candidates);
    }

    [Fact]
    public void GecmisVeConfirmedKayitlar_AdayOlamaz()
    {
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });
        // Geçmiş provisional
        _db.Matches.Add(new Match
        {
            Id = 8001, ExternalMatchId = "gecmis", LeagueId = SuperLig, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = NowUtc.AddDays(-1), Status = "NotStarted",
            KickoffPrecision = KickoffPrecisions.Provisional
        });
        // Gelecek ama Confirmed
        _db.Matches.Add(new Match
        {
            Id = 8002, ExternalMatchId = "kesin", LeagueId = SuperLig, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = NowUtc.AddDays(2), Status = "NotStarted",
            KickoffPrecision = KickoffPrecisions.Confirmed
        });
        // Ufkun ötesinde provisional
        _db.Matches.Add(new Match
        {
            Id = 8003, ExternalMatchId = "uzak", LeagueId = SuperLig, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = NowUtc.AddDays(30), Status = "NotStarted",
            KickoffPrecision = KickoffPrecisions.Provisional
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var candidates = new FixtureSyncRepository(_db).GetFutureScheduleRefreshCandidates(
            NowUtc, NowUtc.AddDays(8), LockedCompetitions.All.ToList());

        Assert.Empty(candidates);
    }

    [Fact]
    public void GercekOgleMaci_SirfSaati1200Diye_ProvisionalSayilmaz()
    {
        // Damgalamanın kaynağı SAAT DEĞİL, sağlayıcının "TBD" kodudur.
        var confirmedNoon = ProviderFixture(
            new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc), provisional: false);
        var tbdNoon = ProviderFixture(
            new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc), provisional: true);

        Assert.False(confirmedNoon.KickoffProvisional);
        Assert.True(tbdNoon.KickoffProvisional);
    }

    public void Dispose() => _db.Dispose();
}
