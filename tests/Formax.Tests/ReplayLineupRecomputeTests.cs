using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.Outcomes;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Notifications;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Formax.Tests;

/// <summary>
/// İZOLE GERÇEK OLAY REPLAY — varsayılan koşuda ATLANIR. Yalnız <c>FORMAX_REPLAY_DB</c> adı "ReplayTest" içeren bir SQL Server
/// kopyası verilirse çalışır (üretim DB'sine asla bağlanmaz). Gerçek geçmiş olay: resmî kadro, normal OfficialLineupCollector
/// yoluyla GERÇEK resmî kaynaktan yeniden okunur; ardından kalıcı yenileme kuyruğu, debounce, yeni snapshot ve tekrarlı olay denetlenir.
/// </summary>
public class ReplayLineupRecomputeTests
{
    private readonly ITestOutputHelper _out;
    public ReplayLineupRecomputeTests(ITestOutputHelper output) => _out = output;

    [SkippableFact]
    public async Task GercekResmiKadro_IzoleReplay_KuyrukDebounceYeniSnapshotTekrarYok()
    {
        var dbName = Environment.GetEnvironmentVariable("FORMAX_REPLAY_DB");
        Skip.If(string.IsNullOrWhiteSpace(dbName) || !dbName.Contains("ReplayTest"), "FORMAX_REPLAY_DB (…ReplayTest) verilmedi");
        var matchId = int.Parse(Environment.GetEnvironmentVariable("FORMAX_REPLAY_MATCH") ?? "7947");
        var cs = $"Server=localhost\\SQLEXPRESS;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(cs, o => o.CommandTimeout(300)).Options;
        void Log(string step, object? data) => _out.WriteLine($"== {step}: {JsonSerializer.Serialize(data)}");

        DateTime kickoff;
        using (var db = new FormaxDbContext(options))
        {
            var m = db.Matches.Single(x => x.Id == matchId);
            kickoff = DateTime.SpecifyKind(m.MatchDate, DateTimeKind.Utc);
            // Olay ÖNCESİ duruma dön — YALNIZ replay kopyasında.
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM MatchLineupPlayers WHERE MatchId={0}; DELETE FROM MatchLineups WHERE MatchId={0}; " +
                "UPDATE Matches SET Status='NotStarted', HomeScore=0, AwayScore=0, ResultSource=NULL, ResultUpdatedAtUtc=NULL WHERE Id={0}; " +
                "DELETE FROM MatchPredictionSnapshots WHERE MatchId={0}; DELETE FROM PredictionRecomputeRequests; DELETE FROM PredictionScorecards WHERE MatchId={0}; " +
                "DELETE FROM UserNotifications WHERE MatchId={0};", matchId);
            Log("0.before", new { matchId, kickoff, lineups = db.MatchLineups.Count(l => l.MatchId == matchId), snapshots = db.MatchPredictionSnapshots.Count(s => s.MatchId == matchId) });
        }
        var t = kickoff.AddMinutes(-55);

        MatchPredictionSnapshotService Snapshots(FormaxDbContext db)
        {
            var loader = new OutcomeHistoryLoader(db);
            return new MatchPredictionSnapshotService(db, loader, new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance),
                NullLogger<MatchPredictionSnapshotService>.Instance);
        }
        PredictionRecomputeWorker Worker(FormaxDbContext db) => new(db, Snapshots(db), NullLogger<PredictionRecomputeWorker>.Instance);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectCallback = OfficialNetworkGuard.ConnectGuardedAsync
        };
        using var http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        var limiter = new OfficialHostRateLimiter();
        async Task<LineupMatchOutcome> Collect(DateTime now)
        {
            using var db = new FormaxDbContext(options);
            var fetcher = new OfficialContentFetcher(http, new OfficialSourceStore(db), limiter, new DnsOfficialAddressResolver(), new OfficialFetcherOptions(),
                NullLogger<OfficialContentFetcher>.Instance, null);
            var config = new ConfigurationBuilder().Build();
            var sources = new IOfficialCompetitionSource[] { new SerieASdpSource(fetcher, config), new PremierLeagueSdpSource(fetcher), new TffSource(fetcher) };
            var collector = new OfficialLineupCollector(db, sources, fetcher,
                new MatchNotificationDispatcher(db, new OfficialLineupTests.CountingDelivery(), NullLogger<MatchNotificationDispatcher>.Instance),
                config, NullLogger<OfficialLineupCollector>.Instance);
            return await collector.CollectForMatchAsync(matchId, now);
        }

        // 1) Eski snapshot.
        using (var db = new FormaxDbContext(options))
        {
            var r = await Snapshots(db).RunAsync(kickoff.AddHours(-3));
            Log("1.oldSnapshotCycle", r);
        }
        string oldId;
        using (var db = new FormaxDbContext(options))
        {
            var s = db.MatchPredictionSnapshots.Single(x => x.MatchId == matchId);
            oldId = s.SnapshotId;
            Log("1.oldSnapshot", View(s));
        }

        // 2) Gerçek olay ingestion.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var o2 = await Collect(t);
        Log("2.ingestion", new { o2, ms = sw.ElapsedMilliseconds });
        Assert.Equal(LineupOutcomes.Released, o2.Outcome);
        using (var db = new FormaxDbContext(options))
        {
            var h = db.MatchLineups.Single(x => x.MatchId == matchId);
            Log("2.lineup", new { h.Provider, h.SourceUrl, h.VerificationStatus, h.RawContentHash, h.HomeFormation, h.AwayFormation, players = db.MatchLineupPlayers.Count(p => p.MatchId == matchId),
                sampleHome = db.MatchLineupPlayers.Where(p => p.MatchId == matchId && p.Side == "Home" && p.Role == "Starter").Select(p => p.ShirtNumber + " " + p.PlayerName).Take(11).ToList() });
            var q = db.PredictionRecomputeRequests.Single();
            Log("2.queue", q);
            Assert.Equal(("OfficialLineup", "Pending"), (q.TriggerType, q.Status));
        }

        // 3) Debounce.
        using (var db = new FormaxDbContext(options))
        {
            var r = await Worker(db).RunOnceAsync(t.AddMinutes(1));
            Log("3.debounceTick", new { r.Claimed });
            Assert.Equal(0, r.Claimed);
        }

        // 4) Yeniden hesap (yeni işçi örneği — restart eşdeğeri).
        using (var db = new FormaxDbContext(options))
        {
            var r = await Worker(db).RunOnceAsync(t.AddMinutes(3));
            Log("4.recomputeTick", new { r.Claimed, r.Outcomes });
            Assert.Equal(1, r.Claimed);
        }
        string newId;
        using (var db = new FormaxDbContext(options))
        {
            var rows = db.MatchPredictionSnapshots.AsNoTracking().Where(s => s.MatchId == matchId).OrderBy(s => s.ComputedAtUtc).ToList();
            foreach (var s in rows) Log("4.snapshot", View(s));
            Assert.Equal(2, rows.Count);
            Assert.False(rows[0].IsCurrent);
            Assert.True(rows[1].IsCurrent);
            Assert.Equal(oldId, rows[1].PreviousSnapshotId);
            Assert.Equal("OfficialLineup", rows[1].TriggerType);
            Assert.StartsWith("official:", rows[1].TriggerSource);
            Assert.NotEqual(rows[0].IntelligenceFingerprint, rows[1].IntelligenceFingerprint);
            newId = rows[1].SnapshotId;
            Log("4.queueAfter", db.PredictionRecomputeRequests.AsNoTracking().ToList());

            // 5) Keşfet (liste okuyucu) ve Detay (tekil okuyucu) aynı yeni snapshot.
            var reader = new MatchOutcomeSnapshotReader(db);
            var detail = await reader.GetCurrentAsync(matchId);
            var discover = (await reader.GetCurrentForMatchesAsync(new[] { matchId }))[matchId];
            Log("5.discoverVsDetail", new { detail = detail.SnapshotId, discover = discover.SnapshotId, detail.PredictionEligibility, detail.Status });
            Assert.Equal(newId, detail.SnapshotId);
            Assert.Equal(newId, discover.SnapshotId);
            Assert.Equal(JsonSerializer.Serialize(detail), JsonSerializer.Serialize(discover));
        }

        // 6) Aynı gerçek olay tekrar → ikinci istek/snapshot yok.
        var o6 = await Collect(t.AddMinutes(10));
        using (var db = new FormaxDbContext(options))
        {
            var r = await Worker(db).RunOnceAsync(t.AddMinutes(14));
            Log("6.duplicate", new { o6, r.Claimed, requests = db.PredictionRecomputeRequests.Count(), snapshots = db.MatchPredictionSnapshots.Count(s => s.MatchId == matchId) });
            Assert.Equal(1, db.PredictionRecomputeRequests.Count());
            Assert.Equal(2, db.MatchPredictionSnapshots.Count(s => s.MatchId == matchId));
        }
        using (var db = new FormaxDbContext(options))
        {
            var r = await Snapshots(db).RunAsync(t.AddMinutes(15));
            Log("7.periodicAfter", new { r.Written, count = db.MatchPredictionSnapshots.Count(s => s.MatchId == matchId) });
            Assert.Equal(2, db.MatchPredictionSnapshots.Count(s => s.MatchId == matchId));
        }
    }

    private static object View(Formax.Domain.Entities.MatchPredictionSnapshot s)
    {
        var d = JsonSerializer.Deserialize<OutcomeSnapshotDto>(s.PayloadJson)!;
        return new
        {
            s.SnapshotId, s.IsCurrent, s.PublicationStatus, s.PredictionEligibility, s.PreviousSnapshotId, s.TriggerType, s.TriggerSource, s.TriggeredAtUtc,
            s.IntelligenceFingerprint, result = d.Families.FirstOrDefault()?.Items.Select(i => $"{i.Market} %{i.Probability}"),
            main = d.MainCards.Select(c => $"{c.Market} %{c.Probability}"), d.ReasonCodes, d.EligibilityReasons,
            audit = s.ChangeAuditJson == null ? null : JsonSerializer.Deserialize<OutcomeChangeAudit>(s.ChangeAuditJson)
        };
    }
}
