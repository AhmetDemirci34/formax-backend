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
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KAÇIRILMIŞ MAÇ TELAFİSİ (17.09.2026) — kaynağın maç listesi yalnız GÜNCEL haftayı yayımlıyorsa (TFF) hafta
/// döndükten sonra kaçırılmış maç listede bulunmaz. Telafi yolu, KAYITLI resmî maç kimliğiyle aynı kaynağın maç
/// sayfasını tek GET ile okur; kimlik tahmin edilmez, yön ve tarih yeniden sınanır.
///
/// Fikstürler gerçek TFF sayfalarıdır (haftalık fikstür + Beşiktaş–Erzurumspor FK maç sayfası). İnternet yok.
/// </summary>
public class OfficialResultRecoveryTests
{
    private const int MatchId = 82550;
    private const string MacId = "317819";
    private static readonly DateTime Kickoff = new(2026, 9, 11, 17, 0, 0, DateTimeKind.Utc); // 20:00 İstanbul
    private static readonly DateTime Now = new(2026, 9, 17, 19, 0, 0, DateTimeKind.Utc);

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        // Fikstürler depoya UTF-8 olarak alınmıştır (üretimde TFF windows-1254 gönderir; çeviri indiricide yapılır).
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private sealed class Fetcher : IOfficialContentFetcher
    {
        private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);
        public List<string> Requests { get; } = new();
        public Fetcher Body(string url, string body) { _bodies[url] = body; return this; }

        public Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request.Url);
            return Task.FromResult(_bodies.TryGetValue(request.Url, out var body)
                ? new OfficialFetchResult(request.Url, OfficialFetchOutcomes.Fetched, 200, body, "h" + body.Length, false, true, false, 0)
                : new OfficialFetchResult(request.Url, OfficialFetchOutcomes.HttpError, 404, null, null, false, false, false, 0));
        }

        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Haftalık fikstür GERÇEK 4. hafta sayfasıdır (17.09.2026 ölçümü: 18-20.09 maçları; aranan 11.09 maçı listede YOK).
    /// Maç sayfası kayıtlı resmî kimlikle okunabilir.
    /// </summary>
    private static Fetcher RealPages() => new Fetcher()
        .Body(TffSource.FixturePageUrl, Fixture("tff_weekly_week4.html"))
        .Body(TffSource.MatchPageUrl(MacId), Fixture("tff_match_besiktas_erzurumspor.html"));

    private static OfficialResultBotService Bot(FormaxDbContext db, IOfficialCompetitionSource source)
        => new(db, new[] { source }, new OfficialDataSourceCatalog(db),
            new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance),
            new ConfigurationBuilder().Build(), NullLogger<OfficialResultBotService>.Instance);

    private static string Seed(bool withLink = true, string home = "Beşiktaş", string away = "Erzurumspor FK",
        DateTime? kickoff = null)
    {
        var name = Guid.NewGuid().ToString();
        using var db = Db(name);
        db.Teams.AddRange(new Team { Id = 1, Name = home }, new Team { Id = 2, Name = away });
        db.Matches.Add(new Match
        {
            Id = MatchId, LeagueId = 203, League = "Süper Lig", MatchDate = kickoff ?? Kickoff,
            Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2
        });
        if (withLink)
            db.OfficialMatchLinks.Add(new OfficialMatchLink
            {
                MatchId = MatchId, SourceKey = OfficialSourceRegistry.TffSite, OfficialMatchId = MacId,
                OfficialUrl = TffSource.MatchPageUrl(MacId), OfficialHomeName = "BEŞİKTAŞ A.Ş.",
                OfficialAwayName = "ERZURUMSPOR FK", OfficialKickoffUtc = Kickoff,
                OfficialStatus = OfficialMatchStatuses.Scheduled, LinkedAtUtc = Kickoff, VerifiedAtUtc = Kickoff
            });
        db.SaveChanges();
        return name;
    }

    // ── Maç sayfası ayrıştırıcısı ────────────────────────────────────────────

    [Fact]
    public async Task MacSayfasi_GercekSayfa_SkorVeKickoffOkunur_BeklemePenceresiUygulanir()
    {
        var source = new TffSource(RealPages());
        var read = await source.ReadMatchAsync(MacId, new OfficialRoundContext("r", Now, OfficialPurposes.Result, MatchId));
        Assert.True(read.Ok, read.Detail);
        var record = read.Value!;
        Assert.Equal(("BEŞİKTAŞ A.Ş.", "ERZURUMSPOR FK"), (record.HomeName, record.AwayName));
        Assert.Equal(Kickoff, record.KickoffUtc);
        Assert.Equal((OfficialMatchStatuses.Finished, 3, 0), (record.Status, record.HomeScore, record.AwayScore));

        // Başlama saatinden 120 dk geçmeden skor final sayılmaz (canlı skor olabilir).
        var early = await source.ReadMatchAsync(MacId, new OfficialRoundContext("r2", Kickoff.AddMinutes(90), OfficialPurposes.Result, MatchId));
        Assert.Equal(OfficialMatchStatuses.Unknown, early.Value!.Status);
        Assert.Null(early.Value.HomeScore);

        // Tanınmayan kimlik biçimi istek üretmez.
        var bad = await source.ReadMatchAsync("abc", new OfficialRoundContext("r3", Now, OfficialPurposes.Result, MatchId));
        Assert.Equal(OfficialReadOutcomes.NotSupported, bad.Outcome);
    }

    // ── Telafi yolu ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HaftaDondukten_SonraKacirilmisMac_KayitliKimlikle_MacSayfasindanKapanir()
    {
        var name = Seed();
        var fetcher = RealPages();
        using (var db = Db(name))
        {
            var report = await Bot(db, new TffSource(fetcher)).RunCycleAsync(Now);
            Assert.Equal(OfficialResultWriter.Applied, report.Matches.Single().Outcome);
        }
        using (var db = Db(name))
        {
            var row = db.Matches.Single();
            Assert.Equal(MatchStatuses.Finished, row.Status);
            Assert.Equal((3, 0), (row.HomeScore, row.AwayScore));
            Assert.Equal("official:" + OfficialSourceRegistry.TffSite, row.ResultSource);
            Assert.Equal("Verified", row.ResultVerificationStatus);
        }
        // Liste + maç sayfası; maç sayfası teyit için ikinci kez istenir (tur hafızası üretimde tek indirmeye düşürür).
        Assert.Contains(TffSource.FixturePageUrl, fetcher.Requests);
        Assert.Contains(TffSource.MatchPageUrl(MacId), fetcher.Requests);
    }

    [Fact]
    public async Task TelafiYolu_KayitliKimlikYoksa_IstekUretmez_AsilRetGerekcesiKorunur()
    {
        var name = Seed(withLink: false);
        var fetcher = RealPages();
        using (var db = Db(name))
        {
            var report = await Bot(db, new TffSource(fetcher)).RunCycleAsync(Now);
            Assert.Equal("NoObservation", report.Matches.Single().Outcome);
            Assert.Contains("NoCandidate", report.Matches.Single().Detail);
        }
        Assert.DoesNotContain(TffSource.MatchPageUrl(MacId), fetcher.Requests);
        using var check = Db(name);
        Assert.Equal(MatchStatuses.NotStarted, check.Matches.Single().Status);
        Assert.Equal("IdentityNotMatched", check.MatchResultChecks.Single().LastErrorClass);
    }

    [Fact]
    public async Task TelafiYolu_SicakYoldaCalismaz_BaslamaSaatinden6SaatGecmedikce_MacSayfasiIstenmez()
    {
        var name = Seed();
        var fetcher = RealPages();
        using (var db = Db(name))
            await Bot(db, new TffSource(fetcher)).RunCycleAsync(Kickoff.AddMinutes(110));
        Assert.DoesNotContain(TffSource.MatchPageUrl(MacId), fetcher.Requests);
        using var check = Db(name);
        Assert.Equal(MatchStatuses.NotStarted, check.Matches.Single().Status);
    }

    [Fact]
    public async Task TelafiYolu_TersYon_VeYanlisTarih_Reddedilir_SonucYazilmaz()
    {
        // Kayıtlı kimlik doğru ama FORMAX maçı TERS yönde: sayfadaki ev sahibi eşleşmez.
        var reversed = Seed(home: "Erzurumspor FK", away: "Beşiktaş");
        using (var db = Db(reversed))
        {
            var report = await Bot(db, new TffSource(RealPages())).RunCycleAsync(Now);
            Assert.Equal("NoObservation", report.Matches.Single().Outcome);
            Assert.Contains("OrientationMismatch", report.Matches.Single().Detail);
        }
        using (var db = Db(reversed)) Assert.Equal(MatchStatuses.NotStarted, db.Matches.Single().Status);

        // Kayıtlı kimlik doğru ama FORMAX başlama saati 72 saatlik pencerenin dışında.
        var wrongDate = Seed(kickoff: Kickoff.AddDays(10));
        using (var db = Db(wrongDate))
        {
            var report = await Bot(db, new TffSource(RealPages())).RunCycleAsync(Kickoff.AddDays(11));
            Assert.Contains("WrongDate", report.Matches.Single().Detail);
        }
        using (var db = Db(wrongDate)) Assert.Equal(MatchStatuses.NotStarted, db.Matches.Single().Status);
    }

    [Fact]
    public async Task TelafiYolu_MacSayfasiOkunamazsa_SahteSonucYok_SonrakiDenemePlanlanir()
    {
        var name = Seed();
        var onlyList = new Fetcher().Body(TffSource.FixturePageUrl, Fixture("tff_weekly_week4.html"));
        using (var db = Db(name))
        {
            var report = await Bot(db, new TffSource(onlyList)).RunCycleAsync(Now);
            Assert.Equal("NoObservation", report.Matches.Single().Outcome);
        }
        using var check = Db(name);
        Assert.Equal(MatchStatuses.NotStarted, check.Matches.Single().Status);
        Assert.Null(check.Matches.Single().ResultSource);
        Assert.True(check.MatchResultChecks.Single().NextCheckUtc > Now);
    }

    // ── Kapsam raporu dış kaynağa çıkmaz ─────────────────────────────────────

    /// <summary>
    /// Kapsam raporu (GET /admin/results/organizations) YALNIZ DB okur. Kaynak üzerinden sabitlenir: denetleyicinin
    /// yapıcısı yalnız <c>FormaxDbContext</c> alır ve GET gövdesinde indirici/bot/HttpClient kullanımı yoktur
    /// (bot turu yalnız POST uçlarında [FromServices] ile çözülür).
    /// </summary>
    [Fact]
    public void KapsamRaporu_YalnizDbOkur_IndiriciVeBotBagimliligiYok()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        var source = File.ReadAllText(Path.Combine(dir!, "Formax.API", "Controllers", "Admin", "AdminResultBotController.cs"));

        Assert.Matches(@"public AdminResultBotController\(FormaxDbContext \w+\)", source);
        var organizations = source[source.IndexOf("[HttpGet(\"organizations\")]", StringComparison.Ordinal)..];
        organizations = organizations[..organizations.IndexOf("[HttpGet(\"day\")]", StringComparison.Ordinal)];
        foreach (var forbidden in new[] { "IOfficialContentFetcher", "OfficialResultBotService",
                     "OfficialStatisticsBotService", "HttpClient", "ReadMatchesAsync", "RunCycleAsync" })
            Assert.DoesNotContain(forbidden, organizations);
        // Rapor gerçekten DB tablolarını okur (defter, plan, gözlem, katalog).
        foreach (var table in new[] { "OfficialDataSources", "OfficialSourceFetches", "MatchResultChecks",
                     "MatchResultObservations" })
            Assert.Contains(table, organizations);
    }
}
