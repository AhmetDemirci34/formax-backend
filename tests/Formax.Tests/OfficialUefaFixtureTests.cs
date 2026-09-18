using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// RESMÎ UEFA FİKSTÜR ALIMI (18.09.2026) — UCL (2), UEL (3) ve UECL (848) yaklaşan maçları.
///
/// Fikstür dosyası GERÇEK cevaptan alınmıştır (match.uefa.com/v5/matches?competitionId=1,14,2019 —
/// üç müsabaka tek indirmede geliyor, kayıtlar competition.id ile ayrılıyor). Testlerde GERÇEK AĞ YOK:
/// indirici betimlenmiş cevap döner, DB InMemory.
///
/// Kilitli kural: kimlik ASLA yalnız takım adı değildir. Kanonik maç = organizasyon + SIRALI çift +
/// dar başlama penceresi + TEK aday; kanonik takım = kalıcı sağlayıcı kimliği ya da aynı organizasyonda
/// TEK adaylı ad eşleşmesi. Çift maçlı turun iki ayağı asla aynı kimliğe çözülemez.
/// </summary>
public class OfficialUefaFixtureTests : IDisposable
{
    private const int Ucl = LockedCompetitions.ChampionsLeague;      // 2  ← UEFA competitionId 1
    private const int Uel = LockedCompetitions.EuropaLeague;         // 3  ← 14
    private const int Uecl = LockedCompetitions.ConferenceLeague;    // 848 ← 2019

    private static readonly DateTime Now = new(2026, 9, 18, 9, 0, 0, DateTimeKind.Utc);

    private readonly FormaxDbContext _db = new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase($"uefafx-{Guid.NewGuid():N}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    public void Dispose() => _db.Dispose();

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private static string RealUpcoming => Fixture("uefa_fixtures_upcoming.json");

    // ── Betimlenmiş indirici ─────────────────────────────────────────────────

    private sealed class ScriptedFetcher : IOfficialContentFetcher
    {
        private readonly Func<string, int, (string? Body, string Outcome)> _reply;
        public List<string> Requests { get; } = new();

        public ScriptedFetcher(Func<string, int, (string? Body, string Outcome)> reply) => _reply = reply;

        public static ScriptedFetcher Body(string body) => new((_, _) => (body, OfficialFetchOutcomes.Fetched));
        public static ScriptedFetcher Fail(string outcome) => new((_, _) => (null, outcome));

        public Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request.Url);
            var (body, outcome) = _reply(request.Url, Requests.Count);
            return Task.FromResult(new OfficialFetchResult(request.Url, outcome,
                body == null ? 500 : 200, body, body?.Length.ToString(), false, true, false, 0));
        }

        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    private OfficialUefaFixtureService Service(ScriptedFetcher fetcher, params (string Key, string Value)[] config)
    {
        var settings = config.ToDictionary(c => c.Key, c => (string?)c.Value);
        return new OfficialUefaFixtureService(_db, new UefaMatchApiSource(fetcher),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<OfficialUefaFixtureService>.Instance);
    }

    // ── 1 + 2 + 3 + 4 + 5. Ayrıştırma, organizasyon eşlemesi, saat ──────────

    [Fact]
    public void GercekCevap_UcMusabakaTekIndirmedenAyrilir_TakimKimlikleriVeUtcTasinir()
    {
        var records = UefaMatchApiSource.Parse(RealUpcoming);
        Assert.Equal(6, records.Count);

        var byLeague = records
            .GroupBy(r => UefaMatchApiSource.LeagueByCompetition[int.Parse(r.Extra!["competitionId"])])
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, byLeague[Ucl]);
        Assert.Equal(2, byLeague[Uel]);
        Assert.Equal(2, byLeague[Uecl]);

        // UCL: Fenerbahçe – Slavia Praha, 20.10.2026 16:45Z
        var fener = records.Single(r => r.OfficialMatchId == "2049595");
        Assert.Equal(("Fenerbahçe", "Slavia Praha"), (fener.HomeName, fener.AwayName));
        Assert.Equal(new DateTime(2026, 10, 20, 16, 45, 0, DateTimeKind.Utc), fener.KickoffUtc);
        Assert.Equal(DateTimeKind.Utc, fener.KickoffUtc!.Value.Kind);
        Assert.Equal(OfficialMatchStatuses.Scheduled, fener.Status);
        Assert.Null(fener.HomeScore);
        // UEFA takım kimliği taşınır (isim benzerliğine mecbur kalmamak için).
        Assert.Equal("52692", fener.Extra!["homeTeamId"]);
        Assert.Equal("52498", fener.Extra!["awayTeamId"]);
        Assert.Equal("1", fener.Extra!["competitionId"]);
        // UEFA sezonu BİTİŞ yılıdır: 2026/27 → "2027". Ham hâliyle taşınır.
        Assert.Equal("2027", fener.Extra!["sourceSeasonYear"]);
        Assert.Equal("Matchday 3", fener.Extra!["matchday"]);

        // Türkiye saati: 16:45Z → 19:45 (UTC+3).
        var istanbul = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        Assert.Equal(new DateTime(2026, 10, 20, 19, 45, 0),
            TimeZoneInfo.ConvertTimeFromUtc(fener.KickoffUtc!.Value, istanbul));

        // Organizasyon eşlemesi kilitli kapsamdadır ve ters yön tutarlıdır.
        Assert.Equal(new[] { Ucl, Uel, Uecl }.OrderBy(x => x),
            UefaMatchApiSource.LeagueByCompetition.Values.OrderBy(x => x));
        Assert.All(UefaMatchApiSource.LeagueByCompetition.Values, l => Assert.True(LockedCompetitions.IsLocked(l)));
        Assert.Equal("1,14,2019", UefaMatchApiSource.FixtureCompetitionQuery);
    }

    // ── 6. Sayfalama + tek istek adresi ─────────────────────────────────────

    [Fact]
    public async Task Sayfalama_DoluSayfadaDevamEder_KisaSayfadaDurur()
    {
        var page0 = "[" + string.Join(",", Enumerable.Range(0, 100).Select(i => Record(9000 + i, 1, "2026-10-20T16:45:00Z"))) + "]";
        var page1 = "[" + Record(9500, 14, "2026-10-22T16:45:00Z") + "]";
        var fetcher = new ScriptedFetcher((url, n) => (n == 1 ? page0 : page1, OfficialFetchOutcomes.Fetched));

        var read = await new UefaMatchApiSource(fetcher).ReadFixturesAsync(
            Now.Date, Now.Date.AddDays(90), Round());

        Assert.True(read.Ok);
        Assert.Equal(101, read.Value!.Count);
        Assert.Equal(2, fetcher.Requests.Count);
        Assert.Contains("offset=0", fetcher.Requests[0]);
        Assert.Contains("offset=100", fetcher.Requests[1]);
        Assert.All(fetcher.Requests, u => Assert.StartsWith("https://match.uefa.com/v5/matches?competitionId=1%2C14%2C2019", u));
    }

    // ── 8 + 14 + 15. Boş cevap ve şema hatası: veri silinmez, watermark yok ─

    [Fact]
    public async Task BosCevap_MevcutFiksturleriSilmez_TurBasariliSayilir()
    {
        SeedTeams();
        var existing = SeedMatch(991, Ucl, Now.AddDays(20), 1, 2, scheduleSource: OfficialUefaFixtureService.ScheduleSourceTag);

        var report = await Service(ScriptedFetcher.Body("[]")).ImportAsync(Now);

        Assert.True(report.Succeeded);
        Assert.Equal(0, report.Created);
        Assert.Single(_db.Matches.ToList());
        Assert.Equal(existing, _db.Matches.Single().MatchDate);
    }

    [Fact]
    public async Task SemaHatasi_VeOkumaHatasi_TurBASARISIZ_HicbirSeyYazilmaz()
    {
        SeedTeams();
        var broken = await Service(ScriptedFetcher.Body("{\"matches\":[]}")).ImportAsync(Now);
        Assert.False(broken.Succeeded);
        Assert.Empty(_db.Matches.ToList());

        var failed = await Service(ScriptedFetcher.Fail(OfficialFetchOutcomes.Timeout)).ImportAsync(Now);
        Assert.Equal(UefaFixtureSyncReport.SourceFailed, failed.Outcome);
        Assert.Contains(OfficialFetchOutcomes.Timeout, failed.Detail);
        Assert.Empty(_db.Matches.ToList());
    }

    // ── 9 + 11. Doğrulanmış bağlantıdan takım kimliği + Scheduled yazımı ───

    [Fact]
    public async Task DogrulanmisMacBaglantisi_UefaTakimKimligiOgretir_YaklasanMacYazilir()
    {
        // Geçmiş UCL maçı sonuç botu tarafından ZATEN eşleştirilmiş (OfficialMatchLinks).
        _db.Teams.AddRange(
            new Team { Id = 10, Name = "Fenerbahçe" }, new Team { Id = 11, Name = "Slavia Praha" });
        var past = new Match
        {
            Id = 900, LeagueId = Ucl, League = "UEFA Champions League",
            MatchDate = Now.AddDays(-8), Status = MatchStatuses.Finished,
            HomeTeamId = 10, AwayTeamId = 11, HomeScore = 1, AwayScore = 1,
            ResultSource = "official:uefa-match-api"
        };
        _db.Matches.Add(past);
        _db.OfficialMatchLinks.Add(new OfficialMatchLink
        {
            MatchId = 900, SourceKey = UefaMatchApiSource.Key, OfficialMatchId = "2049500",
            OfficialHomeName = "Fenerbahçe", OfficialAwayName = "Slavia Praha",
            LinkedAtUtc = Now.AddDays(-8), VerifiedAtUtc = Now.AddDays(-8)
        });
        _db.SaveChanges();

        // Kaynak: aynı takımların GEÇMİŞ maçı (kimlik tohumu) + YAKLAŞAN maçı.
        var json = "[" + Record(2049500, 1, Now.AddDays(-8).ToString("yyyy-MM-ddTHH:mm:ssZ"), status: "FINISHED",
                                 homeId: 52692, homeName: "Fenerbahçe", awayId: 52498, awayName: "Slavia Praha")
                   + "," + Record(2049595, 1, "2026-10-20T16:45:00Z",
                                 homeId: 52692, homeName: "Fenerbahçe", awayId: 52498, awayName: "Slavia Praha") + "]";

        var report = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);

        Assert.True(report.Succeeded);
        Assert.Equal(2, report.TeamIdentitiesLearned);
        Assert.Equal(1, report.Created);
        Assert.Equal(0, report.UnresolvedTeams);

        var identities = _db.TeamProviderIdentities.Where(i => i.Provider == TeamIdentityProviders.Uefa).ToList();
        Assert.Equal(2, identities.Count);
        Assert.All(identities, i => Assert.Equal(TeamIdentityEvidence.VerifiedMatchLink, i.MatchedBy));
        Assert.Equal(10, identities.Single(i => i.ProviderTeamId == "52692").TeamId);

        var created = _db.Matches.Single(m => m.Id != 900);
        Assert.Equal((Ucl, 10, 11), (created.LeagueId, created.HomeTeamId, created.AwayTeamId));
        Assert.Equal(new DateTime(2026, 10, 20, 16, 45, 0, DateTimeKind.Utc), created.MatchDate);
        Assert.Equal(MatchStatuses.NotStarted, created.Status);
        Assert.Equal(OfficialUefaFixtureService.ScheduleSourceTag, created.ScheduleSource);
        Assert.Equal(KickoffPrecisions.Confirmed, created.KickoffPrecision);
        Assert.Null(created.ExternalMatchId);          // api-football kimliği henüz YOK
        Assert.Equal(0, created.HomeScore);            // skor uydurulmaz
        Assert.Equal("Matchday 3", created.Round);
        // Resmî kaynak referansı kanonik maça bağlandı.
        Assert.Equal("2049595", _db.OfficialMatchLinks.Single(l => l.MatchId == created.Id).OfficialMatchId);
        // Geçmiş maç DOKUNULMADI (sonuç botunun alanı).
        Assert.Equal(MatchStatuses.Finished, _db.Matches.Single(m => m.Id == 900).Status);
        Assert.Equal(1, _db.Matches.Single(m => m.Id == 900).HomeScore);
    }

    // ── 6. Belirsiz / eşleşmeyen takımda YAZMAMA ────────────────────────────

    [Fact]
    public async Task BelirsizVeyaEslesmeyenTakim_MacYAZILMAZ_TakimYaratilmaz_Raporlanir()
    {
        // ÖLÇÜLMÜŞ GERÇEK BELİRSİZLİK (18.09.2026, canlı kaynak): UEFA "L. Red Imps" / "Lincoln" adı FORMAX'ta
        // hem "Lincoln Red Imps FC" hem "Lincoln" ile eşleşiyor → TEK aday yok. "Kairat Almaty" ise FORMAX'ta hiç yok.
        _db.Teams.AddRange(
            new Team { Id = 20, Name = "Lincoln Red Imps FC" }, new Team { Id = 21, Name = "Lincoln" },
            new Team { Id = 22, Name = "Panathinaikos" });
        _db.Matches.AddRange(
            new Match { Id = 800, LeagueId = Uecl, MatchDate = Now.AddDays(-30), HomeTeamId = 20, AwayTeamId = 22, Status = MatchStatuses.Finished },
            new Match { Id = 801, LeagueId = Uecl, MatchDate = Now.AddDays(-30), HomeTeamId = 21, AwayTeamId = 22, Status = MatchStatuses.Finished });
        _db.SaveChanges();
        var before = _db.Matches.Count();

        var json = "[" + Record(3001, 2019, "2026-10-20T16:45:00Z", homeId: 99001, homeName: "Lincoln", awayId: 50084, awayName: "Panathinaikos")
                   + "," + Record(3002, 2019, "2026-10-22T14:30:00Z", homeId: 79970, homeName: "Kairat Almaty", awayId: 50084, awayName: "Panathinaikos") + "]";

        var report = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);

        Assert.True(report.Succeeded);
        Assert.Equal(0, report.Created);
        Assert.Equal(2, report.UpcomingInScope);
        Assert.Equal(2, report.UnresolvedTeams);
        Assert.Equal(before, _db.Matches.Count());
        Assert.Equal(3, _db.Teams.Count());                       // TAKIM YARATILMADI
        Assert.Contains(report.UnmatchedTeamReport, s => s.Contains("Kairat Almaty") && s.Contains("NoCandidate"));
        Assert.Contains(report.UnmatchedTeamReport, s => s.Contains("Lincoln") && s.Contains("AmbiguousName"));
        // Çözülemeyen takımlar için KİMLİK KAYDI da açılmaz (yanlış eşleme kalıcılaşmaz).
        Assert.DoesNotContain(_db.TeamProviderIdentities.ToList(), i => i.ProviderTeamId is "99001" or "79970");
    }

    [Fact]
    public async Task AyniOrganizasyondaTEKAdayliAdEslesmesi_KimlikKurar_MacYazilir()
    {
        _db.Teams.AddRange(
            new Team { Id = 30, Name = "Crvena Zvezda" }, new Team { Id = 31, Name = "FC Copenhagen" },
            // Başka organizasyonda aynı ada benzeyen takım — kapsam dışı olduğu için belirsizlik YARATMAZ.
            new Team { Id = 32, Name = "Copenhagen" });
        _db.Matches.AddRange(
            new Match { Id = 810, LeagueId = Uecl, MatchDate = Now.AddDays(-25), HomeTeamId = 30, AwayTeamId = 31, Status = MatchStatuses.Finished },
            new Match { Id = 811, LeagueId = LockedCompetitions.PremierLeague, MatchDate = Now.AddDays(-25), HomeTeamId = 32, AwayTeamId = 30, Status = MatchStatuses.Finished });
        _db.SaveChanges();

        var json = "[" + Record(2050331, 2019, "2026-10-22T16:45:00Z",
            homeId: 50069, homeName: "Crvena Zvezda", awayId: 52709, awayName: "FC Copenhagen") + "]";

        var report = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);

        Assert.Equal(1, report.Created);
        var identities = _db.TeamProviderIdentities.ToList();
        Assert.Equal(2, identities.Count);
        Assert.All(identities, i => Assert.Equal(TeamIdentityEvidence.NameUniqueInCompetition, i.MatchedBy));
        var created = _db.Matches.Single(m => m.LeagueId == Uecl && m.MatchDate > Now);
        Assert.Equal((30, 31), (created.HomeTeamId, created.AwayTeamId));   // yön korunur
    }

    // ── 10 + 13. Idempotency ve kickoff değişikliği ─────────────────────────

    [Fact]
    public async Task AyniUefaMacKimligi_IkinciTurda_DuplicateYaratmaz_KickoffDegisirseGunceller()
    {
        SeedIdentities();
        var json = "[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]";
        var first = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);
        Assert.Equal(1, first.Created);
        var matchId = _db.Matches.Single().Id;
        _db.ChangeTracker.Clear();

        // Aynı cevap: yeni satır YOK, değişiklik YOK.
        var second = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now.AddHours(24));
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Unchanged);
        Assert.Single(_db.Matches.ToList());
        Assert.Equal(matchId, _db.Matches.Single().Id);
        _db.ChangeTracker.Clear();

        // Kaynak saati değiştirdi → kanonik maç GÜNCELLENİR, kimlik aynı kalır.
        var moved = "[" + Record(2049595, 1, "2026-10-20T19:00:00Z") + "]";
        var third = await Service(ScriptedFetcher.Body(moved)).ImportAsync(Now.AddHours(48));
        Assert.Equal(0, third.Created);
        Assert.Equal(1, third.Updated);
        var row = Assert.Single(_db.Matches.ToList());
        Assert.Equal(matchId, row.Id);
        Assert.Equal(new DateTime(2026, 10, 20, 19, 0, 0, DateTimeKind.Utc), row.MatchDate);
        Assert.Single(_db.OfficialMatchLinks.ToList());
    }

    // ── 9 (status) + 12. Erteleme / iptal güncellemesi ──────────────────────

    [Theory]
    [InlineData("POSTPONED", MatchStatuses.Postponed)]
    [InlineData("CANCELLED", MatchStatuses.Cancelled)]
    public async Task ErtelenenVeIptalEdilen_KanonikDurumGuncellenir(string sourceStatus, string expected)
    {
        SeedIdentities();
        await Service(ScriptedFetcher.Body("[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]")).ImportAsync(Now);
        _db.ChangeTracker.Clear();
        Assert.Equal(MatchStatuses.NotStarted, _db.Matches.Single().Status);

        var changed = "[" + Record(2049595, 1, "2026-10-20T16:45:00Z", status: sourceStatus) + "]";
        var report = await Service(ScriptedFetcher.Body(changed)).ImportAsync(Now.AddHours(24));

        Assert.Equal(1, report.Updated);
        Assert.Equal(expected, _db.Matches.Single().Status);
        Assert.Single(_db.Matches.ToList());
    }

    // ── 11 + 12. api-football ile duplicate oluşmaması (iki yön) ────────────

    [Fact]
    public void KanonikKimlik_UefaOnce_ApiFootballSonra_AyniMacaCozer()
    {
        var kickoff = new DateTime(2026, 10, 20, 16, 45, 0, DateTimeKind.Utc);
        var uefaRow = new CanonicalMatchCandidate(555, Ucl, 10, 11, kickoff, null);

        // api-football T−1'de aynı maçı kendi kimliğiyle getirir → ADOPT (duplicate açılmaz).
        var adopt = OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, kickoff.AddMinutes(15),
            new[] { uefaRow }, providerExternalId: "1700001");
        Assert.Equal(CanonicalFixtureDecision.Adopt, adopt.Outcome);
        Assert.Equal(555, adopt.MatchId);

        // Ters yön (api-football önce yazmış, UEFA sonra geliyor) → yine ADOPT.
        var apiRow = new CanonicalMatchCandidate(556, Ucl, 10, 11, kickoff, "1700001");
        Assert.Equal(CanonicalFixtureDecision.Adopt,
            OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, kickoff, new[] { apiRow }).Outcome);

        // Kanonik maç yoksa CREATE.
        Assert.Equal(CanonicalFixtureDecision.Create,
            OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, kickoff, Array.Empty<CanonicalMatchCandidate>()).Outcome);
    }

    [Fact]
    public void KanonikKimlik_CiftMaqliTurunIkiAyagi_KARISMAZ_BelirsizdeYazmaz()
    {
        var leg1 = new DateTime(2026, 8, 18, 18, 0, 0, DateTimeKind.Utc);
        var leg2 = leg1.AddDays(8);
        var candidates = new[]
        {
            new CanonicalMatchCandidate(601, Ucl, 10, 11, leg1, "1622621"),   // Fenerbahçe EV
            new CanonicalMatchCandidate(602, Ucl, 11, 10, leg2, "1622630")    // Lyon EV (TERS yön)
        };

        // 1. ayağın kimliği yalnız 1. ayağa çözülür; ters yön asla karışmaz.
        Assert.Equal(601, OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, leg1, candidates).MatchId);
        Assert.Equal(602, OfficialFixtureIdentityPolicy.Resolve(Ucl, 11, 10, leg2, candidates).MatchId);
        // 8 gün uzaktaki aynı yön pencereye girmez → CREATE (yanlış maça bağlanmaz).
        Assert.Equal(CanonicalFixtureDecision.Create,
            OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, leg1.AddDays(8), candidates).Outcome);

        // İki aday aynı pencerede ise DUPLICATE ÜRETİLMEZ.
        var twins = new[]
        {
            new CanonicalMatchCandidate(701, Ucl, 10, 11, leg1, null),
            new CanonicalMatchCandidate(702, Ucl, 10, 11, leg1.AddHours(2), null)
        };
        var ambiguous = OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, leg1, twins);
        Assert.Equal(CanonicalFixtureDecision.Ambiguous, ambiguous.Outcome);
        Assert.Contains("MultipleCandidates", ambiguous.Reason);

        // Tek aday BAŞKA bir api-football kimliği taşıyorsa üzerine yazılmaz.
        var mismatch = OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 11, leg1,
            new[] { new CanonicalMatchCandidate(801, Ucl, 10, 11, leg1, "9999999") }, providerExternalId: "1700001");
        Assert.Equal(CanonicalFixtureDecision.Ambiguous, mismatch.Outcome);
        Assert.Contains("ExternalIdMismatch", mismatch.Reason);

        // Geçersiz takım kimliği hiçbir zaman yazıma dönüşmez.
        Assert.Equal(CanonicalFixtureDecision.Ambiguous,
            OfficialFixtureIdentityPolicy.Resolve(Ucl, 0, 11, leg1, candidates).Outcome);
        Assert.Equal(CanonicalFixtureDecision.Ambiguous,
            OfficialFixtureIdentityPolicy.Resolve(Ucl, 10, 10, leg1, candidates).Outcome);
    }

    [Fact]
    public async Task ApiFootballOnceYazmissa_UefaTuru_IkinciSatirAcmaz_ReferansBaglar()
    {
        SeedIdentities();
        var kickoff = new DateTime(2026, 10, 20, 16, 45, 0, DateTimeKind.Utc);
        _db.Matches.Add(new Match
        {
            Id = 950, ExternalMatchId = "1700001", LeagueId = Ucl, League = "UEFA Champions League",
            MatchDate = kickoff.AddMinutes(-15), Status = MatchStatuses.NotStarted,
            HomeTeamId = 10, AwayTeamId = 11
        });
        _db.SaveChanges();

        var report = await Service(ScriptedFetcher.Body("[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]"))
            .ImportAsync(Now);

        Assert.Equal(0, report.Created);
        Assert.Equal(1, report.Updated);
        var row = Assert.Single(_db.Matches.ToList());
        Assert.Equal(950, row.Id);
        Assert.Equal("1700001", row.ExternalMatchId);                 // api-football kimliği KORUNUR
        Assert.Equal(kickoff, row.MatchDate);                          // resmî saat uygulanır
        Assert.Equal(OfficialUefaFixtureService.ScheduleSourceTag, row.ScheduleSource);
        // Aynı kanonik maça İKİNCİ sağlayıcı referansı bağlandı.
        var link = Assert.Single(_db.OfficialMatchLinks.ToList());
        Assert.Equal((950, "2049595"), (link.MatchId, link.OfficialMatchId));
    }

    // ── 18 + 19 + 20. API-Football isteği yok, diğer ligler ve sonuç korunur ─

    [Fact]
    public async Task UefaFiksturTuru_ApiFootballaCikmaz_DigerLigleriVeKesinSonucuBozmaz()
    {
        SeedIdentities();
        // Başka lig maçı + bitmiş UEFA maçı (kesin sonuçlu).
        _db.Teams.Add(new Team { Id = 40, Name = "Liverpool" });
        _db.Matches.AddRange(
            new Match
            {
                Id = 960, ExternalMatchId = "1600001", LeagueId = LockedCompetitions.PremierLeague,
                League = "Premier League", MatchDate = Now.AddDays(3), Status = MatchStatuses.NotStarted,
                HomeTeamId = 40, AwayTeamId = 10
            },
            new Match
            {
                Id = 961, LeagueId = Ucl, League = "UEFA Champions League", MatchDate = Now.AddDays(-5),
                Status = MatchStatuses.Finished, HomeTeamId = 10, AwayTeamId = 11,
                HomeScore = 2, AwayScore = 1, ResultSource = "official:uefa-match-api",
                ResultVerificationStatus = "Verified"
            });
        _db.SaveChanges();

        var fetcher = ScriptedFetcher.Body("[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]");
        var report = await Service(fetcher).ImportAsync(Now);

        Assert.Equal(1, report.Created);
        Assert.All(fetcher.Requests, u => Assert.StartsWith("https://match.uefa.com/", u));
        Assert.DoesNotContain(fetcher.Requests, u => u.Contains("api-sports.io"));

        var pl = _db.Matches.Single(m => m.Id == 960);
        Assert.Equal((LockedCompetitions.PremierLeague, MatchStatuses.NotStarted, "1600001"),
            (pl.LeagueId, pl.Status, pl.ExternalMatchId));
        var finished = _db.Matches.Single(m => m.Id == 961);
        Assert.Equal((MatchStatuses.Finished, 2, 1), (finished.Status, finished.HomeScore, finished.AwayScore));
        Assert.Equal("official:uefa-match-api", finished.ResultSource);
        Assert.Equal("Verified", finished.ResultVerificationStatus);
    }

    [Fact]
    public async Task GecmisMac_BuServisTarafindanYAZILMAZ_SonucBotununAlani()
    {
        SeedIdentities();
        // Kaynak geçmişte BİTMİŞ bir maç veriyor: fikstür servisi bunu yazmaz.
        var json = "[" + Record(2049400, 1, Now.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"), status: "FINISHED") + "]";

        var report = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);

        Assert.True(report.Succeeded);
        Assert.Equal(0, report.UpcomingInScope);
        Assert.Equal(0, report.Created);
        Assert.Empty(_db.Matches.ToList());
    }

    [Fact]
    public async Task KapsamDisiMusabaka_YAZILMAZ()
    {
        SeedIdentities();
        // competitionId 18 = UEFA Süper Kupa vb. — kilitli kapsamda DEĞİL.
        var json = "[" + Record(2049900, 18, "2026-10-20T16:45:00Z") + "]";

        var report = await Service(ScriptedFetcher.Body(json)).ImportAsync(Now);

        Assert.True(report.Succeeded);
        Assert.Equal(0, report.UpcomingInScope);
        Assert.Empty(_db.Matches.ToList());
    }

    // ── 17. Job: tek uçuş + başarısız turda watermark yazılmaması ───────────

    [Fact]
    public async Task Job_BasarisizTur_WatermarkYazmaz_BasariliTurYazar()
    {
        SeedIdentities();
        var failing = BuildJob(ScriptedFetcher.Fail(OfficialFetchOutcomes.Timeout));
        var failed = await failing.RunOnceAsync(Now);
        Assert.NotNull(failed);
        Assert.False(failed!.Succeeded);
        Assert.Null(failing.LastSuccessUtc);                 // watermark YAZILMADI

        var ok = BuildJob(ScriptedFetcher.Body("[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]"));
        var report = await ok.RunOnceAsync(Now);
        Assert.True(report!.Succeeded);
        Assert.Equal(Now, ok.LastSuccessUtc);
        Assert.Equal(1, report.Created);
    }

    /// <summary>Birinci tur bu indiricide ASENKRON bekler; test ikinci turu bu sırada ister.</summary>
    private sealed class GatedFetcher : IOfficialContentFetcher
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _body;
        public int Calls;

        public GatedFetcher(string body) => _body = body;
        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public async Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
        {
            Calls++;
            _entered.TrySetResult();
            await _release.Task.ConfigureAwait(false);
            return new OfficialFetchResult(request.Url, OfficialFetchOutcomes.Fetched, 200, _body, "h", false, true, false, 0);
        }

        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Job_AyniAndaIkinciTur_ATLANIR_TekUcus()
    {
        SeedIdentities();
        var slow = new GatedFetcher("[" + Record(2049595, 1, "2026-10-20T16:45:00Z") + "]");
        var job = BuildJobWith(slow);

        var first = job.RunOnceAsync(Now);
        await slow.Entered;                                      // birinci tur indiricide bekliyor

        var second = await job.RunOnceAsync(Now);
        Assert.Null(second);                                     // ikinci tur ATLANDI (tek uçuş)

        slow.Release();
        var report = await first;
        Assert.True(report!.Succeeded);
        Assert.Equal(1, slow.Calls);                             // tek indirme
        Assert.Single(_db.Matches.ToList());
    }

    private Formax.Infrastructure.BackgroundJobs.OfficialUefaFixtureSyncJob BuildJob(ScriptedFetcher fetcher)
        => BuildJobWith(fetcher);

    /// <summary>İş, servisi kendi kapsamından çözer — üretimdeki kurulumun aynısı.</summary>
    private Formax.Infrastructure.BackgroundJobs.OfficialUefaFixtureSyncJob BuildJobWith(IOfficialContentFetcher fetcher)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(_db);
        services.AddSingleton(sp => new OfficialUefaFixtureService(
            _db, new UefaMatchApiSource(fetcher), config, NullLogger<OfficialUefaFixtureService>.Instance));
        var sp = services.BuildServiceProvider();
        return new Formax.Infrastructure.BackgroundJobs.OfficialUefaFixtureSyncJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<Formax.Infrastructure.BackgroundJobs.OfficialUefaFixtureSyncJob>.Instance);
    }

    // ── Durum eşlemesi ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(OfficialMatchStatuses.Scheduled, MatchStatuses.NotStarted)]
    [InlineData(OfficialMatchStatuses.Live, MatchStatuses.Live)]
    [InlineData(OfficialMatchStatuses.Postponed, MatchStatuses.Postponed)]
    [InlineData(OfficialMatchStatuses.Cancelled, MatchStatuses.Cancelled)]
    [InlineData(OfficialMatchStatuses.Abandoned, MatchStatuses.Abandoned)]
    public void KaynakDurumu_KanonikDurumaEslenir(string source, string expected)
        => Assert.Equal(expected, OfficialUefaFixtureService.CanonicalStatus(source));

    [Theory]
    [InlineData(OfficialMatchStatuses.Finished)]
    [InlineData(OfficialMatchStatuses.FinishedAfterPenalties)]
    [InlineData(OfficialMatchStatuses.Unknown)]
    public void BitmisVeBilinmeyenDurum_FiksturServisindeYAZILMAZ(string source)
        => Assert.Null(OfficialUefaFixtureService.CanonicalStatus(source));

    // ── Yardımcılar ─────────────────────────────────────────────────────────

    private static OfficialRoundContext Round() => new("uefafx-test", Now, OfficialPurposes.Schedule);

    /// <summary>Gerçek cevabın alan yapısını taşıyan en küçük kayıt.</summary>
    private static string Record(int id, int competitionId, string kickoffIso, string status = "UPCOMING",
        int homeId = 52692, string homeName = "Fenerbahçe", int awayId = 52498, string awayName = "Slavia Praha")
        => "{\"id\":\"" + id + "\",\"seasonYear\":\"2027\",\"status\":\"" + status + "\","
           + "\"competition\":{\"id\":\"" + competitionId + "\"},"
           + "\"kickOffTime\":{\"dateTime\":\"" + kickoffIso + "\",\"utcOffsetInHours\":2},"
           + "\"matchday\":{\"name\":\"MD3\",\"translations\":{\"longName\":{\"EN\":\"Matchday 3\"}}},"
           + "\"round\":{\"metaData\":{\"name\":\"League Phase\"}},"
           + "\"stadium\":{\"translations\":{\"officialName\":{\"EN\":\"Test Arena\"}}},"
           + "\"homeTeam\":{\"id\":\"" + homeId + "\",\"internationalName\":\"" + homeName + "\"},"
           + "\"awayTeam\":{\"id\":\"" + awayId + "\",\"internationalName\":\"" + awayName + "\"},"
           + "\"score\":{}}";

    private void SeedTeams()
    {
        _db.Teams.AddRange(new Team { Id = 1, Name = "Fenerbahçe" }, new Team { Id = 2, Name = "Slavia Praha" });
        _db.SaveChanges();
    }

    private DateTime SeedMatch(int id, int leagueId, DateTime kickoff, int home, int away, string? scheduleSource = null)
    {
        _db.Matches.Add(new Match
        {
            Id = id, LeagueId = leagueId, MatchDate = kickoff, Status = MatchStatuses.NotStarted,
            HomeTeamId = home, AwayTeamId = away, ScheduleSource = scheduleSource
        });
        _db.SaveChanges();
        return kickoff;
    }

    /// <summary>Kalıcı UEFA takım kimlikleri (kimlik yolu testlerin konusu olmadığında).</summary>
    private void SeedIdentities()
    {
        _db.Teams.AddRange(new Team { Id = 10, Name = "Fenerbahçe" }, new Team { Id = 11, Name = "Slavia Praha" });
        _db.TeamProviderIdentities.AddRange(
            new TeamProviderIdentity { TeamId = 10, Provider = TeamIdentityProviders.Uefa, ProviderTeamId = "52692",
                ProviderTeamName = "Fenerbahçe", MatchedBy = TeamIdentityEvidence.VerifiedMatchLink,
                FirstSeenUtc = Now.AddDays(-9), VerifiedAtUtc = Now.AddDays(-9) },
            new TeamProviderIdentity { TeamId = 11, Provider = TeamIdentityProviders.Uefa, ProviderTeamId = "52498",
                ProviderTeamName = "Slavia Praha", MatchedBy = TeamIdentityEvidence.VerifiedMatchLink,
                FirstSeenUtc = Now.AddDays(-9), VerifiedAtUtc = Now.AddDays(-9) });
        _db.SaveChanges();
    }
}
