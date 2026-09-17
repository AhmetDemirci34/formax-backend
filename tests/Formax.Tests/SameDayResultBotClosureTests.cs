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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// AYNI GÜN SONUÇ BOTU KAPANIŞI (17.09.2026). Gerçek internet ve API-Football YOK: resmî kaynak bellek içi kayıt listesidir,
/// DB InMemory, önbellek gerçek <see cref="MemoryCache"/>.
/// </summary>
public class SameDayResultBotClosureTests
{
    // 17.09.2026 20:00 TR başlama (LaLiga, Enabled organizasyon).
    private static readonly DateTime Kickoff = new(2026, 9, 17, 17, 0, 0, DateTimeKind.Utc);
    private const int MatchId = 15469;
    private const int OtherMatchId = 15470;

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private sealed class Source : IOfficialCompetitionSource
    {
        public string SourceKey => OfficialSourceRegistry.LaLigaSite;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };
        public List<OfficialMatchRecord> Records { get; } = new();
        public Func<OfficialRead<IReadOnlyList<OfficialMatchRecord>>>? Override;
        public int Reads;
        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Reads);
            return Task.FromResult(Override?.Invoke() ?? new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Records.ToList(), OfficialReadOutcomes.Ok, null, null));
        }
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));
    }

    private static OfficialResultBotService Bot(FormaxDbContext db, Source source, IMemoryCache? cache = null)
        => new(db, new[] { source }, new OfficialDataSourceCatalog(db),
            new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance,
                cacheInvalidator: cache == null ? null : new MatchResultCacheInvalidator(cache)),
            new ConfigurationBuilder().Build(), NullLogger<OfficialResultBotService>.Instance);

    private static void Seed(FormaxDbContext db, bool neighbour = true)
    {
        db.Teams.AddRange(new Team { Id = 1, Name = "Real Betis" }, new Team { Id = 2, Name = "Getafe" },
            new Team { Id = 3, Name = "Sevilla" }, new Team { Id = 4, Name = "Valencia" });
        db.Matches.AddRange(
            new Match { Id = MatchId, LeagueId = 140, League = "La Liga", MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2 },
            // Betis'in bir sonraki maçı (karar paketi önbelleği bayatlar) ve ilgisiz bir maç.
            new Match { Id = 20001, LeagueId = 140, League = "La Liga", MatchDate = Kickoff.AddDays(4), Status = MatchStatuses.NotStarted, HomeTeamId = 4, AwayTeamId = 1 },
            new Match { Id = 20002, LeagueId = 140, League = "La Liga", MatchDate = Kickoff.AddDays(4), Status = MatchStatuses.NotStarted, HomeTeamId = 3, AwayTeamId = 4 });
        // Aynı saatte başlayan komşu maç (aynı kaynak listesini paylaşır).
        if (neighbour)
            db.Matches.Add(new Match { Id = OtherMatchId, LeagueId = 140, League = "La Liga", MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 3, AwayTeamId = 4 });
        db.SaveChanges();
    }

    private static OfficialMatchRecord Rec(string status, int? h, int? a, string home = "Real Betis Balompié SAD", string away = "Getafe Club de Fútbol SAD",
        string id = "102312", DateTime? kickoff = null)
        => new(OfficialSourceRegistry.LaLigaSite, id, "https://www.laliga.com/partido/" + id, home, away, kickoff ?? Kickoff, status, h, a, status, null, null, null, null);

    private static Task<(string Name, Source Src)> Arrange(params OfficialMatchRecord[] records) => Arrange(true, records);

    private static async Task<(string Name, Source Src)> Arrange(bool neighbour, params OfficialMatchRecord[] records)
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db, neighbour);
        var src = new Source();
        src.Records.AddRange(records);
        await Task.CompletedTask;
        return (name, src);
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ResmiKaynakSonucu_DogruMacaEslesir_KomsuMacinSkoruKarismaz_GozlemKaynakAdresiyleSaklanir()
    {
        var (name, src) = await Arrange(
            Rec(OfficialMatchStatuses.Finished, 3, 3, home: "Sevilla Fútbol Club SAD", away: "Valencia Club de Fútbol SAD", id: "102313"),
            Rec(OfficialMatchStatuses.Finished, 2, 1));
        using (var db = Db(name)) Assert.Equal(2, (await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(112))).Applied);
        using (var db = Db(name))
        {
            var betis = db.Matches.Single(m => m.Id == MatchId);
            var sevilla = db.Matches.Single(m => m.Id == OtherMatchId);
            Assert.Equal((MatchStatuses.Finished, 2, 1, "official:laliga-site"), (betis.Status, betis.HomeScore, betis.AwayScore, betis.ResultSource));
            Assert.Equal((3, 3), (sevilla.HomeScore, sevilla.AwayScore));
            Assert.Equal("102312", db.OfficialMatchLinks.Single(l => l.MatchId == MatchId).OfficialMatchId);

            var obs = db.MatchResultObservations.Single(o => o.MatchId == MatchId);
            Assert.Equal(("Applied", "Accepted", "https://www.laliga.com/partido/102312", "laliga-nextdata-v2"),
                (obs.Decision, obs.ValidationResult, obs.SourceUrl, obs.ParserVersion));
            var check = db.MatchResultChecks.Single(c => c.MatchId == MatchId);
            Assert.Equal(("Resolved", "laliga-site", (string?)null, "Verified"), (check.State, check.LastSourceKey, check.LastErrorClass, check.LastValidationStatus));
            Assert.Equal(Kickoff.AddMinutes(112), check.LastCheckUtc);
        }
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task EvDeplasmanYonu_TersYazilmaz_TersKayitReddedilir()
    {
        // Deplasman galibiyeti: kaynak ev=1 dep=3 → kanonik HomeScore=1, AwayScore=3.
        var (name, src) = await Arrange(Rec(OfficialMatchStatuses.Finished, 1, 3));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(110));
        using (var db = Db(name))
        {
            var m = db.Matches.Single(x => x.Id == MatchId);
            Assert.Equal((1, 3), (m.HomeScore, m.AwayScore));
        }

        // Kaynak aynı maçı ters yönde yayımlıyorsa (Getafe ev sahibi) sonuç YAZILMAZ.
        var (name2, src2) = await Arrange(Rec(OfficialMatchStatuses.Finished, 3, 1, home: "Getafe Club de Fútbol SAD", away: "Real Betis Balompié SAD"));
        using (var db = Db(name2)) await Bot(db, src2).RunCycleAsync(Kickoff.AddMinutes(110));
        using (var db = Db(name2))
        {
            var m = db.Matches.Single(x => x.Id == MatchId);
            Assert.Equal(MatchStatuses.NotStarted, m.Status);
            Assert.Null(m.ResultSource);
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            Assert.Equal(("Pending", "IdentityNotMatched", "Rejected:OrientationMismatch"), (c.State, c.LastErrorClass, c.LastValidationStatus));
        }
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HenuzBitmemisMac_FinalYazilmaz_KickoffOncesiKaynakOkunmaz_SkorsuzBittiKabulEdilmez()
    {
        var (name, src) = await Arrange(Rec(OfficialMatchStatuses.Live, 1, 0));
        using (var db = Db(name))
        {
            var early = await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(90));
            Assert.Equal(0, src.Reads);                                     // maç sürerken kaynak dövülmez
            Assert.Equal(0, early.Claimed);
        }
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
        using (var db = Db(name))
        {
            var m = db.Matches.Single(x => x.Id == MatchId);
            Assert.Equal((MatchStatuses.NotStarted, (string?)null), (m.Status, m.ResultSource));
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            Assert.Equal(("Pending", "NotFinalYet", "NotFinal", Kickoff.AddMinutes(108)), (c.State, c.LastErrorClass, c.LastValidationStatus, c.NextCheckUtc));
        }

        src.Records.Clear();
        src.Records.Add(Rec(OfficialMatchStatuses.Finished, null, null));    // "bitti" ama skor yok → 0-0 uydurulmaz
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(108));
        using (var db = Db(name)) Assert.Equal(MatchStatuses.NotStarted, db.Matches.Single(x => x.Id == MatchId).Status);
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AyniGozlemIkinciKezGelirse_DuplicateKayitVeYenidenYazimYok()
    {
        var (name, src) = await Arrange(false, Rec(OfficialMatchStatuses.Finished, 2, 0));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(110));

        DateTime? writtenAt;
        int observations, recompute;
        using (var db = Db(name))
        {
            writtenAt = db.Matches.Single(x => x.Id == MatchId).ResultUpdatedAtUtc;
            observations = db.MatchResultObservations.Count(o => o.MatchId == MatchId);
            recompute = db.PredictionRecomputeRequests.Count();
        }

        // Aynı resmî kayıt yazıcıya iki kez daha gelir (ör. yönetici yeniden kontrolü, tekrar eden tur).
        for (var i = 1; i <= 2; i++)
        {
            using var db = Db(name);
            var writer = new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance);
            var rec = Rec(OfficialMatchStatuses.Finished, 2, 0);
            var w = await writer.ApplyAsync(MatchId, src, rec, OfficialResultStatusPolicy.Decide(rec), "replay", Kickoff.AddMinutes(110 + 3 * i));
            Assert.Equal(OfficialResultWriter.Unchanged, w.Outcome);
        }
        // Bot turu da bitmiş maçın kaynağını yeniden okumaz.
        var readsBefore = src.Reads;
        using (var db = Db(name)) await Bot(db, src).RecheckNowAsync(new[] { MatchId }, Kickoff.AddMinutes(130));
        Assert.Equal(readsBefore, src.Reads);

        using (var db = Db(name))
        {
            Assert.Equal(writtenAt, db.Matches.Single(x => x.Id == MatchId).ResultUpdatedAtUtc);
            Assert.Equal(observations, db.MatchResultObservations.Count(o => o.MatchId == MatchId));
            Assert.Equal(recompute, db.PredictionRecomputeRequests.Count());
            Assert.Single(db.MatchStatisticsChecks.Where(s => s.MatchId == MatchId));
        }
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task KaynakBulunamaz_YaDaOkunamazsa_SahteSonucYok_HataSinifiSaklanir_SonrakiDenemePlanlanir()
    {
        var (name, src) = await Arrange();   // kaynak listesinde maç yok
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
        using (var db = Db(name))
        {
            var m = db.Matches.Single(x => x.Id == MatchId);
            Assert.Equal((MatchStatuses.NotStarted, 0, 0, (string?)null), (m.Status, m.HomeScore, m.AwayScore, m.ResultSource));
            Assert.Empty(db.MatchResultObservations.Where(o => o.MatchId == MatchId));
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            Assert.Equal(("IdentityNotMatched", "Rejected:NoCandidate", "laliga-site"), (c.LastErrorClass, c.LastValidationStatus, c.LastSourceKey));
            Assert.True(c.NextCheckUtc > Kickoff.AddMinutes(106));
        }

        src.Override = () => new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(null, OfficialReadOutcomes.FetchFailed, "HTTP 503", null);
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(108));
        using (var db = Db(name))
        {
            Assert.Equal(MatchStatuses.NotStarted, db.Matches.Single(x => x.Id == MatchId).Status);
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            Assert.Equal(("SourceReadFailed", "NotChecked"), (c.LastErrorClass, c.LastValidationStatus));
            Assert.Contains("FetchFailed", c.LastOutcome);
            Assert.Equal(Kickoff.AddMinutes(111), c.NextCheckUtc);
        }
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task RestartSonrasi_RetryPlaniDbdenKorunur_ErkenTurIstekUretmez_CokenIscininKilidiDevralinir()
    {
        var (name, src) = await Arrange(false, Rec(OfficialMatchStatuses.Live, 0, 0));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
        Assert.Equal(1, src.Reads);

        // Süreç "çöktü": kilit alınmış ama bırakılmamış satır.
        using (var db = Db(name))
        {
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            c.NextCheckUtc = Kickoff.AddMinutes(108);
            c.LockOwner = "crashed-worker";
            c.LockedUntilUtc = Kickoff.AddMinutes(108) + OfficialResultBotService.LockDuration;
            db.SaveChanges();
        }

        // Yeni süreç, plan zamanından önce: istek yok.
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(107));
        Assert.Equal(1, src.Reads);
        // Plan zamanı geldi ama çöken işçinin kilidi hâlâ geçerli: istek yok.
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(109));
        Assert.Equal(1, src.Reads);

        src.Records.Clear();
        src.Records.Add(Rec(OfficialMatchStatuses.Finished, 0, 1));
        using (var db = Db(name))
        {
            var r = await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(114));   // kilit süresi doldu → devralındı
            Assert.Equal(1, r.Applied);
        }
        using (var db = Db(name))
        {
            var c = db.MatchResultChecks.Single(x => x.MatchId == MatchId);
            Assert.Equal(("Resolved", 2), (c.State, c.AttemptCount));
            Assert.Null(c.LockOwner);
        }
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SonucYazilinca_KararPaketiOnbellegiTemizlenir_ApiOkumaYoluYeniSonucuDondurur()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        foreach (var id in new[] { MatchId, 20001, 20002 })
            cache.Set(global::GetRecommendationFeedUseCase.DecisionPackageCacheKey(id), new object());

        var (name, src) = await Arrange(Rec(OfficialMatchStatuses.Finished, 4, 2));
        var day = new DateOnly(2026, 9, 17);
        using (var db = Db(name)) Assert.Empty(await TestReaders.Results(db).GetResultsAsync(day));

        using (var db = Db(name)) await Bot(db, src, cache).RunCycleAsync(Kickoff.AddMinutes(110));

        Assert.False(cache.TryGetValue(global::GetRecommendationFeedUseCase.DecisionPackageCacheKey(MatchId), out _));
        Assert.False(cache.TryGetValue(global::GetRecommendationFeedUseCase.DecisionPackageCacheKey(20001), out _)); // Betis'in sonraki maçı
        Assert.True(cache.TryGetValue(global::GetRecommendationFeedUseCase.DecisionPackageCacheKey(20002), out _));  // ilgisiz maç korunur

        using (var db = Db(name))
        {
            var row = Assert.Single(await TestReaders.Results(db).GetResultsAsync(day), r => r.MatchId == MatchId);
            Assert.Equal((4, 2, "FT"), (row.HomeScore, row.AwayScore, row.ResultDetail));
        }
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task BugunBitenMac_AyniTurkiyeGunundeSonuclarListesineGirer()
    {
        var (name, src) = await Arrange(Rec(OfficialMatchStatuses.Finished, 1, 1));
        var today = new DateOnly(2026, 9, 17);                          // 17:00Z = 20:00 TR
        using (var db = Db(name)) Assert.Empty(await TestReaders.Results(db).GetResultsAsync(today));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(113));   // 21:53 TR, aynı gün
        using (var db = Db(name))
        {
            var results = await TestReaders.Results(db).GetResultsAsync(today);
            var r = Assert.Single(results);
            Assert.Equal((MatchId, "Real Betis", "Getafe", 1, 1), (r.MatchId, r.HomeTeam.Name, r.AwayTeam.Name, r.HomeScore, r.AwayScore));
            var check = db.MatchResultChecks.Single(c => c.MatchId == MatchId);
            Assert.True(check.ResolvedAtUtc - Kickoff <= TimeSpan.FromMinutes(120));
        }
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Fact]
    public void SonucZinciri_ApiFootballIstegiUretmez()
    {
        // (a) Zincirdeki hiçbir sınıf API-Football/sağlayıcı bağımlılığı almaz.
        foreach (var type in new[] { typeof(OfficialResultBotService), typeof(OfficialResultWriter), typeof(MatchResultCacheInvalidator), typeof(OfficialDataSourceCatalog) })
            foreach (var ctor in type.GetConstructors())
                foreach (var p in ctor.GetParameters())
                    Assert.DoesNotMatch("ApiFootball|SportsDataProvider|HttpClient", p.ParameterType.FullName ?? p.ParameterType.Name);

        // (b) Kaynak metninde API-Football çağrısı yok.
        foreach (var file in new[]
        {
            new[] { "Formax.Infrastructure", "OfficialSources", "OfficialResultBotService.cs" },
            new[] { "Formax.Infrastructure", "OfficialSources", "OfficialResultWriter.cs" },
            new[] { "Formax.Infrastructure", "OfficialSources", "MatchResultCacheInvalidator.cs" },
            new[] { "Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs" },
            new[] { "Formax.Application", "Services", "OfficialSources", "OfficialResultPolicies.cs" }
        })
        {
            var text = Repo(file);
            foreach (var forbidden in new[] { "ApiFootball", "ISportsDataProvider", "api-sports", "v3.football" })
                Assert.DoesNotContain(forbidden, text);
        }

        // (c) Sonuç amaçlı doğrulanmış kaynakların hiçbiri API-Football host'u değildir.
        foreach (var league in LockedCompetitions.All)
            foreach (var d in OfficialSourceRegistry.VerifiedFor(league, OfficialPurposes.Result))
                Assert.DoesNotContain(d.Hosts, h => h.Contains("api-sports", StringComparison.OrdinalIgnoreCase) || h.Contains("api-football", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DenemeSiniflamasi_HerSonucIcinHataSinifiVeDogrulamaDurumu()
    {
        Assert.Equal(new ResultAttemptClassification(null, "Verified"), ResultAttemptClassifier.Classify("ResultApplied", null));
        Assert.Equal(new ResultAttemptClassification("NotFinalYet", "NotFinal"), ResultAttemptClassifier.Classify("NotFinalYet", null));
        Assert.Equal(new ResultAttemptClassification("CircuitOpen", "NotChecked"), ResultAttemptClassifier.Classify("NoObservation", "uefa-match-api:CircuitOpen"));
        Assert.Equal(new ResultAttemptClassification("IdentityNotMatched", "Rejected:WrongDate"), ResultAttemptClassifier.Classify("NoObservation", "laliga-site:WrongDate"));
        Assert.Equal(new ResultAttemptClassification("SourceReadFailed", "NotChecked"), ResultAttemptClassifier.Classify("NoObservation", "laliga-site:ParseFailed:x"));
        Assert.Equal(new ResultAttemptClassification("NoOfficialSource", "NotChecked"), ResultAttemptClassifier.Classify("ResultSourceUnavailable", null));
        Assert.Equal(new ResultAttemptClassification("Conflict", "Conflict"), ResultAttemptClassifier.Classify("ResultConflict", null));
        Assert.Equal(new ResultAttemptClassification("VerificationPending", "Pending"), ResultAttemptClassifier.Classify("VerificationPending", null));
    }

    private static string Repo(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray()));
    }
}
