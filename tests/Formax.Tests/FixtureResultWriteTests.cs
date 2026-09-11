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
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// SONUÇ YAZMA SÖZLEŞMESİ — GERÇEK <see cref="FixtureSyncJob"/> turu, sahte
/// sağlayıcı/depo ile çalıştırılır. Hiçbir gerçek API çağrısı yapılmaz.
/// </summary>
public class FixtureResultWriteTests
{
    private const int League = 88;

    // ── Sahteler ──────────────────────────────────────────────────────────────

    private sealed class FakeLock : IFixtureSyncLockRepository
    {
        public Task<bool> TryAcquireAsync(string id, TimeSpan staleness, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task HeartbeatAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProvider : StubSportsDataProvider
    {
        private readonly List<SportsFixtureResult> _fixtures;
        public FakeProvider(params SportsFixtureResult[] fixtures) => _fixtures = fixtures.ToList();

        public override Task<SportsFixtureDayBatch> GetFixturesForDatesAsync(
            IReadOnlyList<DateTime> dates, CancellationToken ct = default)
        {
            var batch = new SportsFixtureDayBatch { RequestedDayCount = dates.Count };
            foreach (var d in dates) batch.SucceededDates.Add(d.Date);
            batch.Fixtures.AddRange(_fixtures);
            return Task.FromResult(batch);
        }
    }

    private sealed class FakeRepo : IFixtureSyncRepository
    {
        public Dictionary<string, Team> Teams = new();
        public Dictionary<string, Match> Matches = new();
        public List<Match> Added = new();

        public Dictionary<string, Team> GetTeamsByExternalIds(IEnumerable<string> ids)
        {
            var set = ids.ToHashSet();
            return Teams.Where(kv => set.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public Dictionary<string, Match> GetMatchesByExternalIds(IEnumerable<string> ids)
        {
            var set = ids.ToHashSet();
            return Matches.Where(kv => set.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public List<Match> GetStaleResultCandidates(DateTime now, int margin, IReadOnlyCollection<int> leagues)
            => new();
        public HashSet<int> GetLeaguesWithVerifiedSeason() => new() { League };

        // Bu testlerin konusu yazma sözleşmesi; hız sınırı ayrı testlerde ölçülür.
        public bool TryReserveFixtureAttempt(string id, string purpose, TimeSpan cd, int cap, DateTime now) => true;
        public void RecordFixtureAttemptOutcome(string id, string purpose, DateTime now, string outcome) { }
        public Task<int> GetFixtureAttemptCountAsync(string id, string purpose, CancellationToken ct = default)
            => Task.FromResult(0);
        public DateTime? GetLastFixtureAttemptUtc(string id, string purpose) => null;
        public Formax.Application.Services.PostMatch.FixtureAttemptSummary GetFixtureAttemptSummary(string id, string purpose)
            => Formax.Application.Services.PostMatch.FixtureAttemptSummary.None;
        public void RecordFixtureAttemptBlocked(string id, string purpose, DateTime nowUtc, string outcome) { }
        public List<Match> GetFutureScheduleRefreshCandidates(
            DateTime nowUtc, DateTime horizonUtc, IReadOnlyCollection<int> leagueIds) => new();
        public void AddTeam(Team t) => Teams[t.ExternalTeamId!] = t;
        public void AddMatch(Match m) { Added.Add(m); Matches[m.ExternalMatchId!] = m; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStats : IMatchLiveStatsRepository
    {
        public MatchLiveStats? GetByMatchId(int id) => null;
        public List<MatchLiveStats> GetByMatchIds(IEnumerable<int> ids) => new();
        public Task UpsertAsync(MatchLiveStats s, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertAsync(MatchLiveStats s, MatchLiveStats? e, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SpyStandings : ILeagueStandingsService
    {
        public List<int> RefreshedLeagues = new();
        public Task RefreshForSettledMatchAsync(int leagueId, DateTime d, CancellationToken ct = default)
        { RefreshedLeagues.Add(leagueId); return Task.CompletedTask; }

        public LeagueStandingsSnapshotDto? GetCached(int l, int s) => null;
        public Task<LeagueStandingsSnapshotDto?> GetAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<LeagueStandingsSnapshotDto?> RefreshAsync(int l, int s, CancellationToken ct = default)
            => Task.FromResult<LeagueStandingsSnapshotDto?>(null);
        public Task<int> RefreshCurrentSeasonAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    // ── Kurulum ───────────────────────────────────────────────────────────────

    private static readonly DateTime Kickoff = new(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc);

    private static SportsFixtureResult Incoming(string status, int? home, int? away) => new()
    {
        ExternalMatchId = "7001",
        MatchDate = Kickoff,
        Status = status,
        HomeScore = home,
        AwayScore = away,
        LeagueName = "Eredivisie",
        LeagueExternalId = League,
        HomeTeamExternalId = "1",
        HomeTeamName = "Ev",
        AwayTeamExternalId = "2",
        AwayTeamName = "Dep"
    };

    private static Match Stored(string status, int home = 0, int away = 0) => new()
    {
        Id = 42,
        ExternalMatchId = "7001",
        Status = status,
        HomeScore = home,
        AwayScore = away,
        LeagueId = League,
        HomeTeamId = 1,
        AwayTeamId = 2,
        MatchDate = Kickoff
    };

    private static (FixtureSyncJob job, SpyStandings standings) Build(
        SportsFixtureResult incoming, Match stored)
    {
        var repo = new FakeRepo
        {
            Teams =
            {
                ["1"] = new Team { Id = 1, ExternalTeamId = "1", Name = "Ev" },
                ["2"] = new Team { Id = 2, ExternalTeamId = "2", Name = "Dep" }
            },
            Matches = { ["7001"] = stored }
        };
        var standings = new SpyStandings();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiFootball:Timezone"] = "UTC",
            // Uzlaştırma bu testlerin konusu değil — kapatılır ki tur sade kalsın.
            ["ApiFootball:ResultReconciliation:MaxDaysPerCycle"] = "0",
            ["ApiFootball:ResultReconciliation:MaxFixtureLookupsPerCycle"] = "0"
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IFixtureSyncLockRepository>(new FakeLock());
        services.AddSingleton<ISportsDataProvider>(new FakeProvider(incoming));
        services.AddSingleton<IFixtureSyncRepository>(repo);
        services.AddSingleton<IMatchLiveStatsRepository>(new FakeStats());
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(new ApiFootballMetrics());
        services.AddSingleton<ILeagueStandingsService>(standings);

        var sp = services.BuildServiceProvider();
        var job = new FixtureSyncJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FixtureSyncJob>.Instance);

        return (job, standings);
    }

    private static Task RunCycle(FixtureSyncJob job)
        => job.RunBackfillAsync(Kickoff.Date, Kickoff.Date);

    // ── Testler ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task KesinSonucYazilir_Damgalanir_VeStandingsTetiklenir()
    {
        var stored = Stored(MatchStatuses.NotStarted);
        var (job, standings) = Build(Incoming("Finished", 3, 1), stored);

        await RunCycle(job);

        Assert.Equal(MatchStatuses.Finished, stored.Status);
        Assert.Equal(3, stored.HomeScore);
        Assert.Equal(1, stored.AwayScore);
        Assert.NotNull(stored.ResultUpdatedAtUtc);
        Assert.Contains("api-football", stored.ResultSource!);
        Assert.Contains(League, standings.RefreshedLeagues);   // ZİNCİR: sonuç → standings
    }

    [Fact]
    public async Task FinishedMac_NotStartedDurumunaGeriDusurulemez()
    {
        var stored = Stored(MatchStatuses.Finished, 2, 0);
        var (job, _) = Build(Incoming("NotStarted", null, null), stored);

        await RunCycle(job);

        Assert.Equal(MatchStatuses.Finished, stored.Status);   // geri alma REDDEDİLDİ
        Assert.Equal(2, stored.HomeScore);
        Assert.Equal(0, stored.AwayScore);
    }

    [Fact]
    public async Task FinishedMac_LiveDurumunaGeriDusurulemez()
    {
        var stored = Stored(MatchStatuses.Finished, 1, 1);
        var (job, _) = Build(Incoming("Live", null, null), stored);

        await RunCycle(job);

        Assert.Equal(MatchStatuses.Finished, stored.Status);
        Assert.Equal(1, stored.HomeScore);
        Assert.Equal(1, stored.AwayScore);
    }

    [Fact]
    public async Task SkorsuzSaglayiciCevabi_FinishedYazmaz()
    {
        // Sağlayıcı "Finished" diyor ama skor vermiyor → 0-0 UYDURULMAZ.
        var stored = Stored(MatchStatuses.NotStarted);
        var (job, standings) = Build(Incoming("Finished", null, null), stored);

        await RunCycle(job);

        Assert.Null(stored.ResultUpdatedAtUtc);       // sonuç damgası YOK
        Assert.Equal(0, stored.HomeScore);            // skor yazılmadı
        Assert.Equal(0, stored.AwayScore);
        Assert.Empty(standings.RefreshedLeagues);     // sonuç yok → zincir tetiklenmez

        // EN ÖNEMLİSİ: skoru olmayan bir maç "Finished" DAMGALANAMAZ. Aksi hâlde tablo
        // onu oynanmış sayar ve 0-0 uydurma bir sonuç olarak puan durumuna girer.
        Assert.NotEqual(MatchStatuses.Finished, stored.Status);
    }

    [Fact]
    public async Task ErtelenmisMac_FinishedYapilmaz()
    {
        var stored = Stored(MatchStatuses.NotStarted);
        var (job, standings) = Build(Incoming("Postponed", null, null), stored);

        await RunCycle(job);

        Assert.Equal(MatchStatuses.Postponed, stored.Status);
        Assert.Null(stored.ResultUpdatedAtUtc);
        Assert.Empty(standings.RefreshedLeagues);
    }

    [Fact]
    public async Task IptalEdilmisMac_FinishedYapilmaz()
    {
        var stored = Stored(MatchStatuses.NotStarted);
        var (job, standings) = Build(Incoming("Cancelled", null, null), stored);

        await RunCycle(job);

        Assert.Equal(MatchStatuses.Cancelled, stored.Status);
        Assert.Null(stored.ResultUpdatedAtUtc);
        Assert.Empty(standings.RefreshedLeagues);
    }
}
