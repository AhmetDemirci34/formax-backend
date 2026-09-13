using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Notifications;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Formax.Tests;

/// <summary>
/// CANLI RESMÎ KAYNAK DOĞRULAMASI — varsayılan test koşusunda ATLANIR.
/// Yalnız <c>FORMAX_LIVE_OFFICIAL=1</c> iken gerçek resmî host'lara gider; DB bellektedir
/// (üretim verisine yazmaz). Üretimdeki indirici kurulumunun (SocketsHttpHandler + bağlantı
/// anında IP koruması) aynısı kullanılır. API-Football'a hiç çıkmaz.
/// </summary>
public class LiveOfficialSourceTests
{
    private readonly ITestOutputHelper _out;
    public LiveOfficialSourceTests(ITestOutputHelper output) => _out = output;

    private static bool Enabled => Environment.GetEnvironmentVariable("FORMAX_LIVE_OFFICIAL") == "1";

    [SkippableFact]
    public async Task Canli_ResmiKadrolar_SerieA_Tff_PremierLeague()
    {
        Skip.IfNot(Enabled, "FORMAX_LIVE_OFFICIAL=1 değil");

        var options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"live-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        var cases = new (int Id, int League, string Home, string Away, DateTime Kickoff)[]
        {
            (15383, 135, "Venezia", "Fiorentina", new DateTime(2026, 9, 11, 18, 45, 0, DateTimeKind.Utc)),
            (82550, 203, "Beşiktaş", "Erzurumspor FK", new DateTime(2026, 9, 11, 17, 0, 0, DateTimeKind.Utc)),
            (107278, 39, "Liverpool", "Fulham", new DateTime(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc)),
        };
        using (var seed = new FormaxDbContext(options))
        {
            var t = 1;
            foreach (var c in cases)
            {
                seed.Teams.Add(new Team { Id = t, Name = c.Home });
                seed.Teams.Add(new Team { Id = t + 1, Name = c.Away });
                seed.Matches.Add(new Match { Id = c.Id, LeagueId = c.League, MatchDate = c.Kickoff,
                    Status = MatchStatuses.NotStarted, HomeTeamId = t, AwayTeamId = t + 1 });
                t += 2;
            }
            seed.SaveChanges();
        }

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectCallback = OfficialNetworkGuard.ConnectGuardedAsync
        };
        using var http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        var limiter = new OfficialHostRateLimiter();

        foreach (var c in cases)
        {
            using var db = new FormaxDbContext(options);
            var fetcher = new OfficialContentFetcher(http, new OfficialSourceStore(db), limiter,
                new DnsOfficialAddressResolver(), new OfficialFetcherOptions(),
                NullLogger<OfficialContentFetcher>.Instance, null);
            var config = new ConfigurationBuilder().Build();
            var sources = new IOfficialCompetitionSource[]
            {
                new SerieASdpSource(fetcher, config), new PremierLeagueSdpSource(fetcher), new TffSource(fetcher)
            };
            var collector = new OfficialLineupCollector(db, sources, fetcher,
                new MatchNotificationDispatcher(db, new OfficialLineupTests.CountingDelivery(),
                    NullLogger<MatchNotificationDispatcher>.Instance),
                config, NullLogger<OfficialLineupCollector>.Instance);

            var outcome = await collector.CollectForMatchAsync(c.Id, DateTime.UtcNow);
            _out.WriteLine($"{c.Id} {c.Home}–{c.Away}: {outcome.Outcome} ({outcome.SourceKey}) {outcome.Detail}");
            foreach (var f in db.OfficialSourceFetches.Where(x => x.MatchId == c.Id || x.RoundKey!.Contains(c.Id.ToString())).OrderBy(x => x.Id))
                _out.WriteLine($"   {f.Host} {f.Purpose} {f.HttpStatus} {f.Outcome} cache={f.CacheHit} {f.Bytes}B cand={f.CandidateCount} acc={f.AcceptedCount} {f.Decision}");

            var header = db.MatchLineups.SingleOrDefault(h => h.MatchId == c.Id);
            var players = db.MatchLineupPlayers.Where(p => p.MatchId == c.Id).ToList();
            _out.WriteLine($"   header={header?.VerificationStatus} home={header?.HomeFormation}/{header?.HomeCoach} away={header?.AwayFormation}/{header?.AwayCoach} " +
                           $"starters={players.Count(p => p.Role == "Starter")} bench={players.Count(p => p.Role == "Bench")}");
            _out.WriteLine("   " + string.Join(", ", players.Where(p => p.Side == "Home" && p.Role == "Starter").Select(p => $"{p.ShirtNumber} {p.PlayerName}")));
            Assert.Equal(LineupOutcomes.Released, outcome.Outcome);
            Assert.Equal(22, players.Count(p => p.Role == "Starter"));
        }
    }
}
