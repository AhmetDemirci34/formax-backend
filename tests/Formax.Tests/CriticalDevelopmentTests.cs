using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Notifications;
using Formax.Infrastructure.OfficialSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KRİTİK GELİŞME MOTORU — yalnız doğrulanmış resmî kaynağın yapılandırılmış maç verisi; söylenti,
/// yanlış maç ve tekrar reddedilir; bildirim yalnız aktif takipçiye, tercihe saygıyla, doğru rotayla.
/// </summary>
public class CriticalDevelopmentTests
{
    private static readonly DateTime Now = new(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Kickoff = new(2026, 9, 13, 18, 45, 0, DateTimeKind.Utc);
    private const int MatchId = 8180;

    private sealed class Env
    {
        private readonly string _name = $"critical-{Guid.NewGuid():N}";
        public OfficialLineupTests.FakeSource Source { get; } = new();
        public OfficialLineupTests.CountingDelivery Delivery { get; } = new();

        public Env(string status = MatchStatuses.NotStarted)
        {
            using var db = NewDb();
            db.Teams.AddRange(new Team { Id = 1, Name = "Sassuolo" }, new Team { Id = 2, Name = "Juventus" });
            db.Matches.Add(new Match { Id = MatchId, LeagueId = 135, League = "Serie A", MatchDate = Kickoff, Status = status, HomeTeamId = 1, AwayTeamId = 2 });
            db.SaveChanges();
        }

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        public void Feed(string status, DateTime? kickoff = null, string venue = "Mapei Stadium", string home = "Sassuolo", string away = "Juventus", string raw = "")
        {
            Source.Records.Clear();
            Source.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "sdp-sas-juv",
                "https://www.legaseriea.it/serie-a/match/x/sassuolo-vs-juventus", home, away, kickoff ?? Kickoff, status, null, null, raw, venue));
        }

        public async Task<MatchCentreRoundReport> RunAsync(DateTime? now = null, IOfficialCompetitionSource? source = null)
        {
            using var db = NewDb();
            var svc = new OfficialMatchCentreService(db, new[] { source ?? Source },
                new MatchNotificationDispatcher(db, Delivery, NullLogger<MatchNotificationDispatcher>.Instance),
                new ConfigurationBuilder().Build(), NullLogger<OfficialMatchCentreService>.Instance);
            return await svc.RunRoundAsync(now ?? Now);
        }

        public void Follow(int userId, bool active = true)
        {
            using var db = NewDb();
            db.UserMatchFollows.Add(new UserMatchFollow { UserId = userId, MatchId = MatchId, IsActive = active });
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task ResmiKaynak_Erteleme_Kabul_TakipciyeTekBildirim_DogruRota()
    {
        var env = new Env();
        env.Follow(7);
        env.Follow(8, active: false);
        env.Feed(OfficialMatchStatuses.Scheduled);
        await env.RunAsync(); // ilk gözlem: bağlantı + durum kaydı, gelişme yok
        env.Feed(OfficialMatchStatuses.Postponed, raw: "POSTPONED");
        var r = await env.RunAsync(Now.AddMinutes(10));

        Assert.Equal(1, r.DevelopmentsRecorded);
        using var db = env.NewDb();
        var d = Assert.Single(db.MatchCriticalDevelopments);
        Assert.Equal(CriticalDevelopmentTypes.Postponed, d.DevelopmentType);
        Assert.Equal(CriticalSeverities.Critical, d.Severity);
        Assert.Equal("Verified", d.VerificationStatus);
        Assert.Equal("https://www.legaseriea.it/serie-a/match/x/sassuolo-vs-juventus", d.OfficialUrl);
        Assert.Equal("Sassuolo–Juventus maçı resmî kaynağa göre ertelendi.", d.SummaryTr);

        var n = Assert.Single(db.UserNotifications);
        Assert.Equal(7, n.UserId);
        Assert.Equal(MatchNotificationTypes.CriticalUpdate, n.NotificationType);
        Assert.Equal("Maçla ilgili kritik gelişme", n.Title);
        Assert.Equal("/match/8180", n.Route);
        Assert.Equal($"MATCH_CRITICAL_UPDATE:8180:{d.EvidenceHash}:7", n.IdempotencyKey);
    }

    [Fact]
    public async Task AyniGelisme_TekrarBildirilmez_KaynakTekrarYeniOlayDegil()
    {
        var env = new Env();
        env.Follow(7);
        env.Feed(OfficialMatchStatuses.Scheduled);
        await env.RunAsync();
        env.Feed(OfficialMatchStatuses.Postponed);
        await env.RunAsync(Now.AddMinutes(10));
        await env.RunAsync(Now.AddMinutes(20)); // kaynak aynı durumu yeniden bildiriyor
        await env.RunAsync(Now.AddMinutes(30));
        using var db = env.NewDb();
        Assert.Single(db.MatchCriticalDevelopments);
        Assert.Single(db.UserNotifications);
    }

    [Fact]
    public async Task YanlisMac_Reddedilir_GelismeYazilmaz()
    {
        var env = new Env();
        env.Follow(7);
        env.Feed(OfficialMatchStatuses.Postponed, home: "Juventus", away: "Sassuolo"); // ters yön
        var r = await env.RunAsync();
        Assert.Equal("IdentityRejected", r.Matches.Single().Outcome);
        using var db = env.NewDb();
        Assert.Empty(db.MatchCriticalDevelopments);
        Assert.Empty(db.UserNotifications);
    }

    [Fact]
    public async Task Soylenti_DogrulanmamisKaynak_HicOkunmaz()
    {
        var env = new Env();
        env.Follow(7);
        var rumor = new OfficialLineupTests.FakeSource { SourceKey = "transfer-rumours" };
        rumor.Records.Add(new OfficialMatchRecord("transfer-rumours", "r1", "https://example.com/rumour", "Sassuolo", "Juventus",
            Kickoff, OfficialMatchStatuses.Postponed, null, null, null));
        await env.RunAsync(source: rumor);
        Assert.Equal(0, rumor.FeedReads);

        // Kayıt defterinde olup doğrulanmamış (NeedsManualReview) kaynak da okunmaz.
        var unverified = new OfficialLineupTests.FakeSource { SourceKey = "venezia-site" };
        await env.RunAsync(source: unverified);
        Assert.Equal(0, unverified.FeedReads);
        using var db = env.NewDb();
        Assert.Empty(db.MatchCriticalDevelopments);
    }

    [Fact]
    public async Task BildirimAyariKapaliysa_Bildirim_Yok_TakipEtmeyeneYok()
    {
        var env = new Env();
        env.Follow(7);
        using (var db = env.NewDb())
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = 7, PrefKey = "match:8180", Enabled = false });
            db.SaveChanges();
        }
        env.Feed(OfficialMatchStatuses.Scheduled);
        await env.RunAsync();
        env.Feed(OfficialMatchStatuses.Cancelled, raw: "CANCELED");
        await env.RunAsync(Now.AddMinutes(10));
        using var db2 = env.NewDb();
        Assert.Single(db2.MatchCriticalDevelopments);
        Assert.Empty(db2.UserNotifications);
    }

    [Fact]
    public async Task BaslamaSaatiDegisti_ResmiSaatMacaYazilir_BildirimSinirliDegil_UctenSonraDurur()
    {
        var env = new Env();
        env.Follow(7);
        env.Feed(OfficialMatchStatuses.Scheduled);
        await env.RunAsync();

        var times = new[] { Kickoff.AddHours(2), Kickoff.AddHours(3), Kickoff.AddHours(4), Kickoff.AddHours(5) };
        var t = Now;
        foreach (var k in times)
        {
            t = t.AddMinutes(10);
            env.Feed(OfficialMatchStatuses.Scheduled, kickoff: k);
            await env.RunAsync(t);
        }
        using var db = env.NewDb();
        Assert.Equal(4, db.MatchCriticalDevelopments.Count(d => d.DevelopmentType == CriticalDevelopmentTypes.KickoffChanged));
        Assert.Equal(3, db.UserNotifications.Count()); // normal şartlarda maç başına en fazla 3
        Assert.Equal("NotificationCapReached", db.MatchCriticalDevelopments.OrderBy(d => d.Id).Last().NotificationNote);
        var match = db.Matches.Single();
        Assert.Equal(times[^1], match.MatchDate);
        Assert.Equal("official:seriea-sdp", match.ScheduleSource);
        Assert.Contains("olarak güncellendi", db.UserNotifications.First().Message);

        // Erteleme (Critical) sınırdan bağımsızdır.
        env.Feed(OfficialMatchStatuses.Postponed, kickoff: times[^1]);
        await env.RunAsync(t.AddMinutes(10));
        using var db2 = env.NewDb();
        Assert.Equal(4, db2.UserNotifications.Count());
    }

    [Fact]
    public async Task IlkGozlemde_ZatenErtelenmisMac_YeniGelismeSayilmaz()
    {
        var env = new Env(status: "Postponed");
        env.Follow(7);
        env.Feed(OfficialMatchStatuses.Postponed);
        await env.RunAsync();
        using var db = env.NewDb();
        Assert.Empty(db.MatchCriticalDevelopments);
    }

    [Fact]
    public void Dedektor_StatDegisikligi_YalnizIkiResmiGozlemArasinda()
    {
        var rec = new OfficialMatchRecord(OfficialSourceRegistry.SerieASdp, "m", null, "Sassuolo", "Juventus",
            Kickoff, OfficialMatchStatuses.Scheduled, null, null, null, "Stadio Città del Tricolore");
        Assert.Empty(CriticalDevelopmentDetector.Detect(1, "Sassuolo", "Juventus", Kickoff, null, rec, Now));
        var changed = CriticalDevelopmentDetector.Detect(1, "Sassuolo", "Juventus", Kickoff,
            new OfficialObservedState(Kickoff, "Mapei Stadium", OfficialMatchStatuses.Scheduled), rec, Now);
        var v = Assert.Single(changed);
        Assert.Equal(CriticalDevelopmentTypes.VenueChanged, v.Type);
        Assert.DoesNotContain(changed, c => c.Type == CriticalDevelopmentTypes.KickoffChanged);
    }

    [Fact]
    public void LisansliSaglayiciSenkronu_ResmiSaatiGeriAlmaz()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        var src = File.ReadAllText(Path.Combine(dir!, "Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"));
        Assert.Contains("existingMatch.ScheduleSource?.StartsWith(\"official:\", StringComparison.Ordinal) != true", src);
    }
}
