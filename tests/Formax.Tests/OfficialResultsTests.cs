using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

namespace Formax.Tests;

/// <summary>
/// AYNI GÜN SONUÇLARI — RESMÎ KAYNAK. Sonuç/durum, olay ve istatistik API-Football'dan
/// alınmaz; resmî lig maç merkezinin gerçek (kaydedilmiş) cevaplarıyla ayrıştırılır ve
/// yalnız resmî kaynak "bitti" dediğinde maç Finished olur.
/// </summary>
public class OfficialResultsTests
{
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    // ── Ayrıştırıcılar (gerçek resmî cevaplar) ────────────────────────────────

    [Fact]
    public void Bundesliga_MacGunuSayfasi_BitenMaclarSkorlu_DevamEdenSkorsuz()
    {
        var records = BundesligaSiteSource.ParseMatchdayPage(Fixture("bundesliga_matchday.html"));

        Assert.Equal(9, records.Count);
        var dortmund = records.Single(r => r.OfficialMatchId == "DFL-MAT-J043GT");
        Assert.Equal("Borussia Dortmund", dortmund.HomeName);
        Assert.Equal("SC Paderborn 07", dortmund.AwayName);
        Assert.Equal(OfficialMatchStatuses.Finished, dortmund.Status);
        Assert.Equal((3, 0), (dortmund.HomeScore, dortmund.AwayScore));
        Assert.Equal((1, 0), (dortmund.HalfTimeHome, dortmund.HalfTimeAway));
        Assert.Equal(new DateTime(2026, 9, 12, 13, 30, 0, DateTimeKind.Utc), dortmund.KickoffUtc);

        // İlk yarısı süren maç "bitti" sayılmaz ve skoru SONUÇ olarak taşınmaz.
        var live = records.Single(r => r.OfficialMatchId == "DFL-MAT-J043H1");
        Assert.Equal(OfficialMatchStatuses.Live, live.Status);
        Assert.Null(live.HomeScore);
        Assert.Null(live.AwayScore);
    }

    [Fact]
    public void PremierLeague_Olaylar_IsimleriResmiKadrodan_TekilAnahtarli()
    {
        var events = PremierLeagueSdpSource.ParseEvents(
            Fixture("pl_events_arsenal_coventry.json"), Fixture("pl_lineups_arsenal_coventry.json"));

        var goals = events.Where(e => e.EventType == "Goal").ToList();
        Assert.Equal(3, goals.Count);
        Assert.All(goals, g => Assert.Equal("home", g.Side));
        Assert.Equal(new[] { 15, 23, 49 }, goals.Select(g => g.Minute).ToArray());
        Assert.All(goals, g => Assert.False(string.IsNullOrWhiteSpace(g.PlayerName)));
        Assert.Equal(2, events.Count(e => e.EventType == "Card"));
        Assert.Equal(9, events.Count(e => e.EventType == "subst"));
        // Aynı olay iki kez üretilmez.
        Assert.Equal(events.Count, events.Select(e => e.OfficialEventId).Distinct().Count());
    }

    [Fact]
    public void PremierLeague_Istatistik_KaynakVermeyenAlanNull_SifirUydurulmaz()
    {
        var stats = PremierLeagueSdpSource.ParseStatistics(Fixture("pl_stats_arsenal_coventry.json"));

        Assert.NotNull(stats);
        Assert.Equal(64, stats!.Home.BallPossession);
        Assert.Equal(20, stats.Home.TotalShots);
        Assert.Equal(6, stats.Home.ShotsOnTarget);
        Assert.Equal(8, stats.Home.Corners);
        Assert.Equal(5, stats.Home.Offsides);
        Assert.Equal(4, stats.Away.TotalShots);
        // Deplasman ofsaydı resmî cevapta YOK → null (0 değil).
        Assert.Null(stats.Away.Offsides);
        Assert.Null(stats.Home.PassAccuracy);
    }

    [Fact]
    public void SerieA_Olaylar_KadroCevabindan_BilinenSozluk()
    {
        var events = SerieASdpSource.ParseEvents(Fixture("seriea_lineups_venezia_fiorentina.json"));

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Contains(e.EventType, new[] { "Goal", "Card", "subst" }));
        Assert.All(events, e => Assert.Contains(e.Side, new[] { "home", "away" }));
        Assert.Equal(events.Count, events.Select(e => e.OfficialEventId).Distinct().Count());
    }

    [Fact]
    public void Kayit_BundesligaSonucIcinDogrulanmis_OlayIstatistikIcinDegil()
    {
        var bl = OfficialSourceRegistry.ByKey(OfficialSourceRegistry.BundesligaSite)!;
        Assert.Equal(OfficialSourceStatuses.Verified, bl.Status);
        Assert.Contains(OfficialPurposes.Result, bl.Capabilities);
        Assert.DoesNotContain(OfficialPurposes.Events, bl.Capabilities);
        Assert.DoesNotContain(OfficialPurposes.Statistics, bl.Capabilities);
        // Serie A istatistik yayımlamıyor → istatistik amacı yok.
        Assert.DoesNotContain(OfficialPurposes.Statistics, OfficialSourceRegistry.ByKey(OfficialSourceRegistry.SerieASdp)!.Capabilities);
    }

    // ── Sonuç uygulama ────────────────────────────────────────────────────────

    private static readonly DateTime Kickoff = new(2026, 9, 13, 13, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Kickoff.AddHours(2.5);
    private const int MatchId = 9101;

    private sealed class ResultSource : IOfficialCompetitionSource, IOfficialResultConfirmation, IOfficialPostMatchSource
    {
        public string SourceKey { get; init; } = OfficialSourceRegistry.BundesligaSite;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };
        public List<OfficialMatchRecord> Records { get; } = new();
        public bool ConfirmationEnabled { get; set; }
        public (int, int)? PageScore { get; set; }
        public int EventReads, StatReads, Confirmations;
        public IReadOnlyList<OfficialMatchEvent> Events { get; set; } = Array.Empty<OfficialMatchEvent>();
        public OfficialMatchStatistics? Stats { get; set; }
        public bool StatsSupported { get; set; } = true;

        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Records, OfficialReadOutcomes.Ok, null, null));

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        public Task<OfficialRead<(int Home, int Away)?>> ConfirmScoreAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            Confirmations++;
            if (!ConfirmationEnabled) throw new InvalidOperationException("teyit bu kaynakta beklenmiyor");
            return Task.FromResult(new OfficialRead<(int Home, int Away)?>(PageScore, OfficialReadOutcomes.Ok, null, null));
        }

        public Task<OfficialRead<IReadOnlyList<OfficialMatchEvent>>> ReadEventsAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            EventReads++;
            return Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchEvent>>(Events, OfficialReadOutcomes.Ok, null, null));
        }

        public Task<OfficialRead<OfficialMatchStatistics>> ReadStatisticsAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            StatReads++;
            return Task.FromResult(StatsSupported
                ? new OfficialRead<OfficialMatchStatistics>(Stats, OfficialReadOutcomes.Ok, null, null)
                : new OfficialRead<OfficialMatchStatistics>(null, OfficialReadOutcomes.NotSupported, null, null));
        }
    }

    /// <summary>Resmî teyit arayüzü OLMAYAN kaynak (Bundesliga / Premier League gibi).</summary>
    private sealed class PlainSource : IOfficialCompetitionSource
    {
        public ResultSource Inner { get; } = new();
        public string SourceKey => Inner.SourceKey;
        public IReadOnlyCollection<string> Purposes => Inner.Purposes;
        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
            => Inner.ReadMatchesAsync(round, ct);
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Inner.ReadLineupAsync(match, round, ct);
    }

    private sealed class Env
    {
        private readonly string _name = $"results-{Guid.NewGuid():N}";
        public OfficialLineupTests.CountingDelivery Delivery { get; } = new();

        public Env(int leagueId, string home, string away, string status = MatchStatuses.NotStarted, int hs = 0, int aws = 0)
        {
            using var db = NewDb();
            db.Teams.AddRange(new Team { Id = 1, Name = home }, new Team { Id = 2, Name = away });
            db.Matches.Add(new Match
            {
                Id = MatchId, LeagueId = leagueId, League = "L", MatchDate = Kickoff, Status = status,
                HomeTeamId = 1, AwayTeamId = 2, HomeScore = hs, AwayScore = aws, ExternalMatchId = "af-1"
            });
            db.SaveChanges();
        }

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        public async Task<MatchCentreRoundReport> CentreAsync(IOfficialCompetitionSource source, DateTime? now = null)
        {
            using var db = NewDb();
            var svc = new OfficialMatchCentreService(db, new[] { source },
                new MatchNotificationDispatcher(db, Delivery, NullLogger<MatchNotificationDispatcher>.Instance),
                new ConfigurationBuilder().Build(), NullLogger<OfficialMatchCentreService>.Instance);
            return await svc.RunRoundAsync(now ?? Now);
        }

        public async Task<OfficialPostMatchCycleResult> PostMatchAsync(IOfficialCompetitionSource source, DateTime? now = null)
        {
            using var db = NewDb();
            var svc = new OfficialPostMatchDataService(db, new[] { source }, new ConfigurationBuilder().Build(),
                NullLogger<OfficialPostMatchDataService>.Instance);
            return await svc.RunCycleAsync(now ?? Now);
        }

        public Match Match() { using var db = NewDb(); return db.Matches.AsNoTracking().Single(m => m.Id == MatchId); }
    }

    private static OfficialMatchRecord Bl(string status, int? hs, int? aws, int? hht = null, int? aht = null) =>
        new(OfficialSourceRegistry.BundesligaSite, "DFL-MAT-J043GT", "https://www.bundesliga.com/de/bundesliga/spieltag/2026-2027/3/x",
            "Borussia Dortmund", "SC Paderborn 07", Kickoff, status, hs, aws, status, null, hht, aht);

    [Fact]
    public async Task ResmiKaynakBittiDerse_SonucYazilir_KaynakVeDogrulamaDamgalanir()
    {
        var env = new Env(78, "Borussia Dortmund", "SC Paderborn");
        var src = new PlainSource();
        src.Inner.Records.Add(Bl(OfficialMatchStatuses.Finished, 3, 0, 1, 0));

        var report = await env.CentreAsync(src);

        var m = env.Match();
        Assert.Equal(MatchStatuses.Finished, m.Status);
        Assert.Equal((3, 0), (m.HomeScore, m.AwayScore));
        Assert.Equal((1, 0), (m.HalfTimeHomeScore, m.HalfTimeAwayScore));
        Assert.Equal("official:bundesliga-site", m.ResultSource);
        Assert.Equal("Verified", m.ResultVerificationStatus);
        Assert.NotNull(m.ResultUpdatedAtUtc);
        Assert.Equal(1, report.ResultsApplied);

        // İkinci tur aynı sonucu yeniden YAZMAZ.
        var again = await env.CentreAsync(src, Now.AddMinutes(10));
        Assert.Equal(0, again.ResultsApplied);
        Assert.Contains(again.Matches, o => o.Outcome == "ResultUnchanged");
    }

    [Fact]
    public async Task ResmiKaynakDevamEdiyorDerse_MacBittiSayilmaz_SkorYazilmaz()
    {
        var env = new Env(78, "Borussia Dortmund", "SC Paderborn");
        var src = new PlainSource();
        src.Inner.Records.Add(Bl(OfficialMatchStatuses.Live, null, null));

        await env.CentreAsync(src);

        var m = env.Match();
        Assert.Equal(MatchStatuses.Live, m.Status);
        Assert.Null(m.ResultUpdatedAtUtc);
        Assert.Null(m.ResultSource);
    }

    [Fact]
    public async Task KesinSonuc_ResmiListeDahaSonraDegisseDeGeriAlinmaz()
    {
        var env = new Env(78, "Borussia Dortmund", "SC Paderborn", MatchStatuses.Finished, 3, 0);
        var src = new PlainSource();
        src.Inner.Records.Add(Bl(OfficialMatchStatuses.Scheduled, null, null));

        await env.CentreAsync(src);

        var m = env.Match();
        Assert.Equal(MatchStatuses.Finished, m.Status);
        Assert.Equal((3, 0), (m.HomeScore, m.AwayScore));
    }

    [Fact]
    public async Task TffListeVeMacSayfasiCelisirse_VerificationPending_SonucKesinlesmez()
    {
        var env = new Env(203, "Beşiktaş", "Erzurumspor");
        var src = new ResultSource { SourceKey = OfficialSourceRegistry.TffSite, ConfirmationEnabled = true, PageScore = (2, 2) };
        src.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.TffSite, "295512", null, "BEŞİKTAŞ A.Ş.",
            "ERZURUMSPOR FK", Kickoff, OfficialMatchStatuses.Finished, 2, 1, null));

        var report = await env.CentreAsync(src);

        var m = env.Match();
        Assert.Equal(MatchStatuses.NotStarted, m.Status);
        Assert.Equal("VerificationPending", m.ResultVerificationStatus);
        Assert.Null(m.ResultSource);
        Assert.Contains(report.Matches, o => o.Outcome == "VerificationPending");
        Assert.Equal(1, src.Confirmations);
    }

    [Fact]
    public async Task TffListeVeMacSayfasiAyniysa_SonucYazilir()
    {
        var env = new Env(203, "Beşiktaş", "Erzurumspor");
        var src = new ResultSource { SourceKey = OfficialSourceRegistry.TffSite, ConfirmationEnabled = true, PageScore = (2, 1) };
        src.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.TffSite, "295512", null, "BEŞİKTAŞ A.Ş.",
            "ERZURUMSPOR FK", Kickoff, OfficialMatchStatuses.Finished, 2, 1, null));

        await env.CentreAsync(src);

        var m = env.Match();
        Assert.Equal(MatchStatuses.Finished, m.Status);
        Assert.Equal((2, 1), (m.HomeScore, m.AwayScore));
        Assert.Equal("official:tff-site", m.ResultSource);
    }

    // ── Olay + istatistik ─────────────────────────────────────────────────────

    private static void MarkOfficialFinished(Env env, string sourceKey, string officialId)
    {
        using var db = env.NewDb();
        var m = db.Matches.Single(x => x.Id == MatchId);
        m.Status = MatchStatuses.Finished; m.HomeScore = 3; m.AwayScore = 0;
        m.ResultSource = "official:" + sourceKey; m.ResultVerificationStatus = "Verified";
        db.OfficialMatchLinks.Add(new OfficialMatchLink
        {
            MatchId = MatchId, SourceKey = sourceKey, OfficialMatchId = officialId,
            OfficialHomeName = "Arsenal", OfficialAwayName = "Coventry City", OfficialKickoffUtc = Kickoff,
            LinkedAtUtc = Now, VerifiedAtUtc = Now
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task OlayVeIstatistik_ResmiKaynaktanBirKezYazilir_TekrarYok_NullKorunur()
    {
        var env = new Env(39, "Arsenal", "Coventry");
        MarkOfficialFinished(env, OfficialSourceRegistry.PremierLeagueSdp, "pl-1");
        var src = new ResultSource
        {
            SourceKey = OfficialSourceRegistry.PremierLeagueSdp,
            Events = PremierLeagueSdpSource.ParseEvents(Fixture("pl_events_arsenal_coventry.json"), Fixture("pl_lineups_arsenal_coventry.json")),
            Stats = PremierLeagueSdpSource.ParseStatistics(Fixture("pl_stats_arsenal_coventry.json"))
        };

        var first = await env.PostMatchAsync(src);
        var second = await env.PostMatchAsync(src, Now.AddMinutes(30));

        using var db = env.NewDb();
        var events = db.MatchEventRecords.Where(e => e.MatchId == MatchId).ToList();
        var stats = db.MatchTeamStatistics.Where(s => s.MatchId == MatchId).ToList();
        Assert.Equal(src.Events.Count, events.Count);
        Assert.Equal(events.Count, events.Select(e => e.ProviderEventId).Distinct().Count());
        Assert.All(events, e => Assert.Equal("official:premier-league-sdp", e.Source));
        Assert.Equal(2, stats.Count);
        Assert.Null(stats.Single(s => s.Side == "Away").Offsides);
        Assert.Equal(5, stats.Single(s => s.Side == "Home").Offsides);
        Assert.Equal(first.EventRowsWritten, events.Count);
        Assert.Equal(0, second.EventRowsWritten);
        Assert.Equal(0, second.StatisticsRowsWritten);
        // Veri yazıldıktan sonra kaynak yeniden SORULMAZ.
        Assert.Equal(1, src.EventReads);
        Assert.Equal(1, src.StatReads);
    }

    [Fact]
    public async Task IstatistikYayimlamayanKaynak_SatirYazilmaz_SifirUydurulmaz()
    {
        var env = new Env(135, "Venezia", "Fiorentina");
        MarkOfficialFinished(env, OfficialSourceRegistry.SerieASdp, "sa-1");
        var src = new ResultSource
        {
            SourceKey = OfficialSourceRegistry.SerieASdp,
            Events = SerieASdpSource.ParseEvents(Fixture("seriea_lineups_venezia_fiorentina.json")),
            StatsSupported = false
        };

        var r = await env.PostMatchAsync(src);

        using var db = env.NewDb();
        Assert.Empty(db.MatchTeamStatistics.Where(s => s.MatchId == MatchId));
        Assert.Equal(0, src.StatReads); // kayıt defteri istatistik amacı vermiyor → sorulmaz
        Assert.Equal(src.Events.Count, db.MatchEventRecords.Count(e => e.MatchId == MatchId));
        Assert.Equal(src.Events.Count, r.EventRowsWritten);
    }

    [Fact]
    public async Task ApiFootballSonucluMac_ResmiOlayAsamasinaAlinmaz()
    {
        var env = new Env(39, "Arsenal", "Coventry", MatchStatuses.Finished, 1, 1);
        using (var db = env.NewDb())
        {
            db.Matches.Single(m => m.Id == MatchId).ResultSource = "api-football:fixtures?date=2026-09-13";
            db.SaveChanges();
        }
        var src = new ResultSource { SourceKey = OfficialSourceRegistry.PremierLeagueSdp };

        var r = await env.PostMatchAsync(src);

        Assert.Equal(0, r.Candidates);
        Assert.Equal(0, src.EventReads);
    }
}
