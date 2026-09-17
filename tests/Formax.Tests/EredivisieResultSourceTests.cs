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
/// EREDIVISIE RESMÎ SONUÇ KAYNAĞI (17.09.2026) — KNVB federasyon sayfalarının ayrıştırıcısı ve zincire bağlanışı.
///
/// Fikstürler GERÇEK sayfalardan alınmıştır (www.knvb.nl/competities/eredivisie/uitslagen ve /programma;
/// 58 + 149 satır). Testlerde internet ve API-Football YOKTUR: indirici betimlenmiş cevaplar döner, DB InMemory.
/// </summary>
public class EredivisieResultSourceTests
{
    private const int League = 88;

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private static string Uitslagen => Fixture("knvb_uitslagen.html");
    private static string Programma => Fixture("knvb_programma.html");

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    /// <summary>Betimlenmiş indirici — adrese göre gövde ya da hata döner; istekleri sayar.</summary>
    private sealed class ScriptedFetcher : IOfficialContentFetcher
    {
        private readonly Dictionary<string, Func<OfficialFetchResult>> _map = new(StringComparer.Ordinal);
        public List<string> Requests { get; } = new();
        public Dictionary<string, int> Hits { get; } = new(StringComparer.Ordinal);

        public ScriptedFetcher Body(string url, string body)
        {
            _map[url] = () => new OfficialFetchResult(url, OfficialFetchOutcomes.Fetched, 200, body,
                body.GetHashCode().ToString("x"), false, true, false, 0);
            return this;
        }

        public ScriptedFetcher Fail(string url, string outcome, int? status = null)
        {
            _map[url] = () => new OfficialFetchResult(url, outcome, status, null, null, false, false, false, 0);
            return this;
        }

        public Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request.Url);
            Hits[request.Url] = Hits.TryGetValue(request.Url, out var n) ? n + 1 : 1;
            return Task.FromResult(_map.TryGetValue(request.Url, out var f)
                ? f()
                : new OfficialFetchResult(request.Url, OfficialFetchOutcomes.HttpError, 404, null, null, false, false, false, 0));
        }

        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ScriptedFetcher RealPages() => new ScriptedFetcher()
        .Body(KnvbSiteSource.ResultsUrl, Uitslagen)
        .Body(KnvbSiteSource.ScheduleUrl, Programma);

    private static OfficialResultBotService Bot(FormaxDbContext db, IOfficialCompetitionSource source)
        => new(db, new[] { source }, new OfficialDataSourceCatalog(db),
            new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance),
            new ConfigurationBuilder().Build(), NullLogger<OfficialResultBotService>.Instance);

    // ── 1. Tamamlanmış normal maç ────────────────────────────────────────────

    [Fact]
    public void Uitslagen_GercekSayfa_BitenMaclarSkorluOkunur_SkorsuzSatirSkorTasimaz()
    {
        var rows = KnvbSiteSource.ParseTimetable(Uitslagen);
        Assert.Equal(58, rows.Count);
        Assert.Equal(54, rows.Count(r => r.HomeScore.HasValue && r.AwayScore.HasValue));
        Assert.Equal(3, rows.Count(r => r.CenterValue == "-"));
        Assert.Single(rows.Where(r => r.StatusTitle != null));
        Assert.All(rows.Where(r => r.CenterValue == "-"), r => Assert.Null(r.HomeScore));

        var ajax = rows.Single(r => r.Date == new DateOnly(2026, 9, 15) && r.HomeName == "Ajax");
        Assert.Equal(("Willem II", 5, 1), (ajax.AwayName, ajax.HomeScore, ajax.AwayScore));
        var psv = rows.Single(r => r.Date == new DateOnly(2026, 9, 13) && r.HomeName == "PSV");
        Assert.Equal(("Sparta Rotterdam", 4, 1), (psv.AwayName, psv.HomeScore, psv.AwayScore));
        var zwolle = rows.Single(r => r.Date == new DateOnly(2026, 9, 13) && r.HomeName == "PEC Zwolle");
        Assert.Equal(("Feyenoord", 0, 7), (zwolle.AwayName, zwolle.HomeScore, zwolle.AwayScore));
        var utrecht = rows.Single(r => r.Date == new DateOnly(2026, 9, 8) && r.HomeName == "FC Utrecht");
        Assert.Equal(("Go Ahead Eagles", 3, 3), (utrecht.AwayName, utrecht.HomeScore, utrecht.AwayScore));
        // 0-0 gerçek skordur, "skor yok" değildir.
        var heerenveen = rows.Single(r => r.Date == new DateOnly(2026, 9, 13) && r.HomeName == "sc Heerenveen");
        Assert.Equal((0, 0), (heerenveen.HomeScore, heerenveen.AwayScore));
        // Resmî kulüp kodu her satırda okunur (kimlik çapası).
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.HomeClubCode) || string.IsNullOrEmpty(r.AwayClubCode)));
    }

    // ── 2. Henüz başlamamış maç ──────────────────────────────────────────────

    [Fact]
    public void Programma_GercekSayfa_BaslamaSaatiAmsterdamYerelindenUtceCevrilir_SkorYok()
    {
        var rows = KnvbSiteSource.ParseTimetable(Programma);
        Assert.Equal(149, rows.Count);
        Assert.All(rows, r => Assert.Null(r.HomeScore));
        Assert.All(rows, r => Assert.NotNull(r.KickoffLocal));

        var groningen = rows.Single(r => r.Date == new DateOnly(2026, 9, 18) && r.HomeName == "FC Groningen");
        Assert.Equal(new TimeOnly(20, 0), groningen.KickoffLocal);
        // 18.09.2026 CEST (UTC+2) → 18:00Z
        Assert.Equal(new DateTime(2026, 9, 18, 18, 0, 0, DateTimeKind.Utc),
            KnvbSiteSource.ToUtc(groningen.Date, groningen.KickoffLocal));
        // Gün adı öneki olan başlık ("vrijdag 18 september 2026") da çözülür.
        Assert.Equal(new DateOnly(2026, 9, 18), KnvbSiteSource.ParseDutchDate("vrijdag 18 september 2026"));
        Assert.Equal(new DateOnly(2027, 1, 31), KnvbSiteSource.ParseDutchDate("31 januari 2027"));
        Assert.Null(KnvbSiteSource.ParseDutchDate("volgende week"));
    }

    [Fact]
    public void Birlestirme_GercekSayfalar_ProgramdakiMacOynanacak_SkorluMacFinalAdayi()
    {
        var records = KnvbSiteSource.Merge(KnvbSiteSource.ParseTimetable(Uitslagen),
            KnvbSiteSource.ParseTimetable(Programma), new DateTime(2026, 9, 17, 19, 0, 0, DateTimeKind.Utc));

        var ajax = records.Single(r => r.HomeName == "Ajax" && r.AwayName == "Willem II");
        Assert.Equal(OfficialMatchStatuses.Finished, ajax.Status);
        Assert.Equal((5, 1), (ajax.HomeScore, ajax.AwayScore));
        Assert.Equal("120", ajax.Extra![OfficialResultSettleGate.ExtraKey]);
        // Yayımlanmayan alan uydurulmaz.
        Assert.Null(ajax.HalfTimeHome);
        Assert.Null(ajax.HalfTimeAway);

        var next = records.Single(r => r.HomeName == "FC Groningen" && r.AwayName == "PEC Zwolle");
        Assert.Equal(OfficialMatchStatuses.Scheduled, next.Status);
        Assert.Null(next.HomeScore);
        Assert.False(next.Extra!.ContainsKey(OfficialResultSettleGate.ExtraKey));
        // Aynı sıralı çift iki kez üretilmez.
        Assert.Equal(records.Count, records.Select(r => r.OfficialMatchId).Distinct().Count());
    }

    // ── 3. Devam eden maç ────────────────────────────────────────────────────

    [Fact]
    public void DevamEdenMac_SkorSayfadaGorunseDe_ProgramdaDuruyorsa_FinalYazilmaz()
    {
        var date = new DateOnly(2026, 9, 19);
        var played = new[] { Row(date, "Ajax", "AAA", "Excelsior", "BBB", "1-0") };
        var upcoming = new[] { Row(date, "Ajax", "AAA", "Excelsior", "BBB", "20:00") };

        var live = KnvbSiteSource.Merge(played, upcoming, new DateTime(2026, 9, 19, 18, 50, 0, DateTimeKind.Utc)).Single();
        Assert.Equal(OfficialMatchStatuses.Unknown, live.Status);
        Assert.Null(live.HomeScore);
        Assert.Equal("NotFinal", OfficialResultStatusPolicy.Decide(live).Kind);

        // Program listesinden düştüğünde final adayı olur.
        var closed = KnvbSiteSource.Merge(played, Array.Empty<KnvbSiteSource.KnvbRow>(),
            new DateTime(2026, 9, 19, 21, 0, 0, DateTimeKind.Utc)).Single();
        Assert.Equal(OfficialMatchStatuses.Finished, closed.Status);
        Assert.Equal((1, 0), (closed.HomeScore, closed.AwayScore));
    }

    [Fact]
    public void BeklemePenceresi_KickoffArti120dkGecmedikce_FinalYazilmaz_SonraYazilir()
    {
        var kickoff = new DateTime(2026, 9, 19, 18, 0, 0, DateTimeKind.Utc);
        var record = KnvbSiteSource.Merge(
            new[] { Row(new DateOnly(2026, 9, 19), "Ajax", "AAA", "Excelsior", "BBB", "2-1") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), kickoff.AddHours(3)).Single();
        var final = OfficialResultStatusPolicy.Decide(record);
        Assert.Equal("Final", final.Kind);

        var early = OfficialResultSettleGate.Apply(final, record, kickoff, kickoff.AddMinutes(100));
        Assert.Equal("NotFinal", early.Kind);
        Assert.Equal(OfficialResultSettleGate.NotSettledReason, early.Reason);
        Assert.Null(early.HomeScore);

        var settled = OfficialResultSettleGate.Apply(final, record, kickoff, kickoff.AddMinutes(121));
        Assert.Equal("Final", settled.Kind);
        Assert.Equal((2, 1), (settled.HomeScore, settled.AwayScore));

        // Durumu açıkça yayımlayan kaynak (bayrak taşımayan kayıt) kapıdan etkilenmez.
        var flagless = record with { Extra = new Dictionary<string, string>() };
        Assert.Equal("Final", OfficialResultSettleGate.Apply(final, flagless, kickoff, kickoff.AddMinutes(10)).Kind);
    }

    // ── 4. Ertelenmiş / yeni tarihe alınmış maç ──────────────────────────────

    [Fact]
    public void SkorsuzSatir_IleriTarihliyseOynanacak_GecmisTarihliyseBelirsiz_ErtelendiUydurulmaz()
    {
        var now = new DateTime(2026, 9, 17, 19, 0, 0, DateTimeKind.Utc);
        var future = KnvbSiteSource.Merge(
            new[] { Row(new DateOnly(2027, 1, 31), "FC Groningen", "AAA", "N.E.C.", "BBB", "-") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), now).Single();
        Assert.Equal(OfficialMatchStatuses.Scheduled, future.Status);
        Assert.Equal("true", future.Extra!["kickoffUnknown"]);

        var past = KnvbSiteSource.Merge(
            new[] { Row(new DateOnly(2026, 9, 5), "FC Groningen", "AAA", "N.E.C.", "BBB", "-") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), now).Single();
        Assert.Equal(OfficialMatchStatuses.Unknown, past.Status);
        // Kaynak erteleme/iptal yayımlamıyor → bu durumlar ÜRETİLMEZ (yanlış "Postponed" yazılmaz).
        Assert.DoesNotContain(OfficialMatchStatuses.Postponed,
            KnvbSiteSource.Merge(KnvbSiteSource.ParseTimetable(Uitslagen), KnvbSiteSource.ParseTimetable(Programma), now)
                .Select(r => r.Status));
        Assert.Equal("NotFinal", OfficialResultStatusPolicy.Decide(past).Kind);
    }

    [Fact]
    public void YaridaKalanMac_GercekSayfa_FederasyonKodundanOkunur_SkorYazilmaz_TekrarOynananMacKarismaz()
    {
        var rows = KnvbSiteSource.ParseTimetable(Uitslagen);
        // ÖLÇÜLDÜ: 05.09.2026 FC Utrecht–Go Ahead Eagles orta hücresi <span title="Gestaakt wegens
        // wanordelijkheden">GWT</span>; aynı çift 08.09'da tekrar oynanmış ve 3-3 bitmiş.
        var stopped = rows.Single(r => r.Date == new DateOnly(2026, 9, 5) && r.HomeName == "FC Utrecht");
        Assert.Equal("Gestaakt wegens wanordelijkheden", stopped.StatusTitle);
        Assert.Equal("GWT", stopped.CenterValue);
        Assert.Null(stopped.HomeScore);
        Assert.Equal(OfficialMatchStatuses.Abandoned, KnvbSiteSource.MapStatusCode(stopped.StatusTitle, stopped.CenterValue));
        Assert.Null(KnvbSiteSource.MapStatusCode(null, "-"));

        var records = KnvbSiteSource.Merge(rows, KnvbSiteSource.ParseTimetable(Programma), Now);
        var pair = records.Where(r => r.HomeName == "FC Utrecht" && r.AwayName == "Go Ahead Eagles").ToList();
        Assert.Equal(2, pair.Count);
        var abandoned = pair.Single(r => r.OfficialMatchId.StartsWith("20260905", StringComparison.Ordinal));
        Assert.Equal(OfficialMatchStatuses.Abandoned, abandoned.Status);
        Assert.Null(abandoned.HomeScore);
        Assert.Equal("Abandoned", OfficialResultStatusPolicy.Decide(abandoned).Kind);
        var replay = pair.Single(r => r.OfficialMatchId.StartsWith("20260908", StringComparison.Ordinal));
        Assert.Equal((OfficialMatchStatuses.Finished, 3, 3), (replay.Status, replay.HomeScore, replay.AwayScore));

        // FORMAX maçı (08.09 12:00Z) yalnız tekrar oynanan satırla eşleşir; yarıda kalan satır pencere dışındadır.
        var decision = OfficialMatchIdentityResolver.Resolve(
            new FormaxMatchIdentity(103837, League, "Utrecht", "GO Ahead Eagles",
                new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc)), pair);
        Assert.True(decision.Accepted, decision.Reason);
        Assert.Equal(replay.OfficialMatchId, decision.Record!.OfficialMatchId);
    }

    // ── 5. Ev/deplasman yönü ─────────────────────────────────────────────────

    [Fact]
    public void EvDeplasmanYonu_TersCevrilmez_TersKayitReddedilir()
    {
        var record = KnvbSiteSource.Merge(
            new[] { Row(new DateOnly(2026, 9, 13), "PEC Zwolle", "ZWO", "Feyenoord", "FEY", "0-7") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc)).Single();
        Assert.Equal(("PEC Zwolle", "Feyenoord", 0, 7), (record.HomeName, record.AwayName, record.HomeScore, record.AwayScore));
        Assert.Equal("20260913:ZWO-FEY", record.OfficialMatchId);

        var identity = new FormaxMatchIdentity(1, League, "PEC Zwolle", "Feyenoord",
            new DateTime(2026, 9, 13, 12, 45, 0, DateTimeKind.Utc));
        Assert.True(OfficialMatchIdentityResolver.Resolve(identity, new[] { record }).Accepted);

        var reversed = new FormaxMatchIdentity(2, League, "Feyenoord", "PEC Zwolle",
            new DateTime(2026, 9, 13, 12, 45, 0, DateTimeKind.Utc));
        Assert.Equal(OfficialIdentityDecision.ReasonOrientationReversed,
            OfficialMatchIdentityResolver.Resolve(reversed, new[] { record }).Reason);
    }

    // ── 6. Takım adı varyasyonu ──────────────────────────────────────────────

    [Theory]
    [InlineData("N.E.C.", "NEC Nijmegen")]
    [InlineData("sc Heerenveen", "Heerenveen")]
    [InlineData("PSV", "PSV Eindhoven")]
    [InlineData("AZ", "AZ Alkmaar")]
    [InlineData("FC Groningen", "Groningen")]
    [InlineData("FC Twente", "Twente")]
    [InlineData("SC Cambuur", "Cambuur")]
    [InlineData("Go Ahead Eagles", "GO Ahead Eagles")]
    [InlineData("FC Utrecht", "Utrecht")]
    [InlineData("Willem II", "Willem II")]
    public void KnvbTakimAdi_FormaxAdiylaEslesir(string official, string formax)
        => Assert.True(OfficialTeamNameMatcher.SameTeam(official, formax), $"{official} ↔ {formax}");

    [Theory]
    [InlineData("Ajax", "Feyenoord")]
    [InlineData("FC Twente", "FC Utrecht")]
    [InlineData("Sparta Rotterdam", "Feyenoord")]
    public void KnvbTakimAdi_BaskaTakimlaEslesmez(string a, string b)
        => Assert.False(OfficialTeamNameMatcher.SameTeam(a, b), $"{a} ↔ {b}");

    // ── 7. Belirsiz eşleşmede yazmama ────────────────────────────────────────

    [Fact]
    public void BelirsizEslesme_AyniCiftIkiKayit_SonucYazilmaz()
    {
        var kickoff = new DateTime(2026, 9, 13, 12, 45, 0, DateTimeKind.Utc);
        var a = KnvbSiteSource.Merge(new[] { Row(new DateOnly(2026, 9, 13), "PEC Zwolle", "Z1", "Feyenoord", "F1", "0-7") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), kickoff.AddHours(4)).Single();
        var b = KnvbSiteSource.Merge(new[] { Row(new DateOnly(2026, 9, 14), "PEC Zwolle", "Z2", "Feyenoord", "F2", "1-1") },
            Array.Empty<KnvbSiteSource.KnvbRow>(), kickoff.AddHours(4)).Single();
        var decision = OfficialMatchIdentityResolver.Resolve(
            new FormaxMatchIdentity(1, League, "PEC Zwolle", "Feyenoord", kickoff), new[] { a, b });
        Assert.False(decision.Accepted);
        Assert.Equal(OfficialIdentityDecision.ReasonAmbiguous, decision.Reason);
    }

    // ── 9. Bozuk HTML · 11. şema değişikliği ─────────────────────────────────

    [Fact]
    public async Task BozukHtml_VeSemaDegisikligi_ParseFailed_MacBulunamadiDiyeGizlenmez()
    {
        var broken = new ScriptedFetcher()
            .Body(KnvbSiteSource.ResultsUrl, "<html><body><div class=\"table-wrapper table-timetable\">kırık")
            .Body(KnvbSiteSource.ScheduleUrl, Programma);
        var read = await new KnvbSiteSource(broken).ReadMatchesAsync(Round());
        Assert.Equal(OfficialReadOutcomes.ParseFailed, read.Outcome);
        Assert.Null(read.Value);

        // Şema değişikliği: satır sınıfı yeniden adlandırılmış → satır bulunamaz, sessizce "maç yok" denmez.
        var renamed = Uitslagen.Replace("class=\"value center\"", "class=\"value result\"", StringComparison.Ordinal);
        Assert.Empty(KnvbSiteSource.ParseTimetable(renamed));
        var schemaChanged = new ScriptedFetcher()
            .Body(KnvbSiteSource.ResultsUrl, renamed)
            .Body(KnvbSiteSource.ScheduleUrl, Programma);
        var schemaRead = await new KnvbSiteSource(schemaChanged).ReadMatchesAsync(Round());
        Assert.Equal(OfficialReadOutcomes.ParseFailed, schemaRead.Outcome);
        Assert.Contains("uitslagen", schemaRead.Detail);

        // Program sayfası şema değiştirirse de sonuç yazılmaz (çelişki kapısı kapalı kalamaz).
        var scheduleBroken = new ScriptedFetcher()
            .Body(KnvbSiteSource.ResultsUrl, Uitslagen)
            .Body(KnvbSiteSource.ScheduleUrl, "<html>bos</html>");
        var scheduleRead = await new KnvbSiteSource(scheduleBroken).ReadMatchesAsync(Round());
        Assert.Equal(OfficialReadOutcomes.ParseFailed, scheduleRead.Outcome);
        Assert.Contains("programma", scheduleRead.Detail);
    }

    // ── 10. Timeout / 5xx / geri çekilme ─────────────────────────────────────

    [Fact]
    public async Task ZamanAsimi_5xx_SonucYazilmaz_SaglikKaydinaYazilir_BesHatadaDevreKesici()
    {
        var timeout = new ScriptedFetcher().Fail(KnvbSiteSource.ResultsUrl, OfficialFetchOutcomes.Timeout);
        var read = await new KnvbSiteSource(timeout).ReadMatchesAsync(Round());
        Assert.Equal(OfficialReadOutcomes.FetchFailed, read.Outcome);
        Assert.Equal(OfficialFetchOutcomes.Timeout, read.Detail);
        // Program sayfası okunamazsa da okuma başarısızdır (yarım veriyle final yazılmaz).
        var half = new ScriptedFetcher().Body(KnvbSiteSource.ResultsUrl, Uitslagen)
            .Fail(KnvbSiteSource.ScheduleUrl, OfficialFetchOutcomes.HttpError, 503);
        Assert.Equal(OfficialReadOutcomes.FetchFailed, (await new KnvbSiteSource(half).ReadMatchesAsync(Round())).Outcome);

        using var db = Db(Guid.NewGuid().ToString());
        await new OfficialDataSourceCatalog(db).EnsureSeededAsync(Now);
        var row = db.OfficialDataSources.Single(s => s.SourceId == OfficialSourceRegistry.KnvbSite);
        for (var i = 1; i <= 5; i++)
            OfficialDataSourceCatalog.Apply(row, OfficialReadOutcomes.FetchFailed, OfficialFetchOutcomes.Timeout, OfficialFetchOutcomes.Timeout, Now);
        Assert.Equal(5, row.ConsecutiveFailureCount);
        Assert.True(OfficialDataSourceCatalog.IsCircuitOpen(row, Now));
        OfficialDataSourceCatalog.Apply(row, OfficialReadOutcomes.Ok, null, OfficialFetchOutcomes.Fetched, Now);
        Assert.Equal((0, false), (row.ConsecutiveFailureCount, OfficialDataSourceCatalog.IsCircuitOpen(row, Now)));
    }

    // ── Tur başına istek birleştirme ─────────────────────────────────────────

    [Fact]
    public async Task TurBasinaIkiSayfa_KaynakListesiTurdaBirKezOkunur_UcuncuIstekYok()
    {
        var fetcher = RealPages();
        using var db = Db(Guid.NewGuid().ToString());
        Seed(db);
        var bot = Bot(db, new KnvbSiteSource(fetcher));
        await bot.RunCycleAsync(Now);

        Assert.Equal(2, fetcher.Requests.Count);
        Assert.Equal(1, fetcher.Hits[KnvbSiteSource.ResultsUrl]);
        Assert.Equal(1, fetcher.Hits[KnvbSiteSource.ScheduleUrl]);
    }

    // ── 8. Idempotanlık · 12. API-Football isteği yok ────────────────────────

    [Fact]
    public async Task GercekSayfadan_KanonikSonucYazilir_IkinciTurIdempotent_DuplicateGozlemYok()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db);

        using (var db = Db(name))
            await Bot(db, new KnvbSiteSource(RealPages())).RunCycleAsync(Now);

        using (var db = Db(name))
        {
            // PEC Zwolle 0-7 Feyenoord (13.09) ve Ajax 5-1 Willem II (15.09) kanonik yazıldı; yön korunmuş.
            var zwolle = db.Matches.Single(m => m.Id == 104630);
            Assert.Equal(MatchStatuses.Finished, zwolle.Status);
            Assert.Equal((0, 7), (zwolle.HomeScore, zwolle.AwayScore));
            Assert.Equal("official:" + OfficialSourceRegistry.KnvbSite, zwolle.ResultSource);
            Assert.Equal("Verified", zwolle.ResultVerificationStatus);
            Assert.Equal(OfficialResultDetails.FullTime, zwolle.ResultDetail);
            var ajax = db.Matches.Single(m => m.Id == 15654);
            Assert.Equal((5, 1), (ajax.HomeScore, ajax.AwayScore));
            // Yaklaşan maça final yazılmaz, 0-0 uydurulmaz, kontrol zamanı gelmemiştir.
            var upcoming = db.Matches.Single(m => m.Id == 104499);
            Assert.Equal(MatchStatuses.NotStarted, upcoming.Status);
            Assert.Null(upcoming.ResultSource);

            var check = db.MatchResultChecks.Single(c => c.MatchId == 104499);
            Assert.Equal(("Pending", 0), (check.State, check.AttemptCount));
            Assert.True(check.NextCheckUtc > Now);
            // Resmî bağlantı kulüp koduyla yazıldı.
            Assert.Equal("20260913:BBKV81X-BBFC26Q", db.OfficialMatchLinks.Single(l => l.MatchId == 104630).OfficialMatchId);
        }

        // Aynı cevap ikinci kez işlenirse: yeniden yazım yok, duplicate gözlem yok.
        using (var db = Db(name))
        {
            var written = db.Matches.Single(m => m.Id == 104630).ResultUpdatedAtUtc;
            var record = KnvbSiteSource.Merge(KnvbSiteSource.ParseTimetable(Uitslagen),
                KnvbSiteSource.ParseTimetable(Programma), Now)
                .Single(r => r.HomeName == "PEC Zwolle" && r.AwayName == "Feyenoord");
            var decision = OfficialResultStatusPolicy.Decide(record);
            var writer = new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance);
            var again = await writer.ApplyAsync(104630, new KnvbSiteSource(RealPages()), record, decision, "round-2", Now.AddMinutes(30));
            Assert.Equal(OfficialResultWriter.Unchanged, again.Outcome);
            Assert.Equal(written, db.Matches.Single(m => m.Id == 104630).ResultUpdatedAtUtc);
        }

        using (var db = Db(name))
        {
            Assert.Single(db.MatchResultObservations.Where(o => o.MatchId == 104630));
            Assert.Empty(db.MatchResultObservations.Where(o => o.ConflictStatus == "Conflict"));
            Assert.Equal("Resolved", db.MatchResultChecks.Single(c => c.MatchId == 104630).State);
        }
    }

    [Fact]
    public async Task SonucZinciri_YalnizKnvbAdresineCikar_ApiFootballIstegiUretmez()
    {
        var fetcher = RealPages();
        using var db = Db(Guid.NewGuid().ToString());
        Seed(db);
        await Bot(db, new KnvbSiteSource(fetcher)).RunCycleAsync(Now);

        Assert.NotEmpty(fetcher.Requests);
        Assert.All(fetcher.Requests, u => Assert.StartsWith("https://www.knvb.nl/", u, StringComparison.Ordinal));
        Assert.DoesNotContain("api-football", SourceFileOf(typeof(KnvbSiteSource)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api-sports.io", SourceFileOf(typeof(KnvbSiteSource)), StringComparison.OrdinalIgnoreCase);
        // Kanonik sonucun kaynak damgası resmîdir (API-Football damgası değil).
        Assert.All(db.Matches.Where(m => m.ResultSource != null),
            m => Assert.StartsWith("official:", m.ResultSource!, StringComparison.Ordinal));
    }

    // ── Kayıt defteri: 11/11 ve yanlış organizasyonda yanlış adaptör ─────────

    [Fact]
    public void Kayit_Kilitli11Organizasyonun_HepsindeDogrulanmisSonucKaynagiVar()
    {
        Assert.Equal(11, OfficialSourceRegistry.LockedLeagueIds.Count);
        foreach (var league in OfficialSourceRegistry.LockedLeagueIds)
            Assert.NotEmpty(OfficialSourceRegistry.VerifiedFor(league, OfficialPurposes.Result));
    }

    [Fact]
    public void KnvbKaynagi_YalnizEredivisiede_DigerOrganizasyonlarda_Calismaz()
    {
        var knvb = OfficialSourceRegistry.ByKey(OfficialSourceRegistry.KnvbSite)!;
        Assert.Equal(new[] { League }, knvb.LeagueIds);
        Assert.Equal(OfficialSourceTier.Federation, knvb.Tier);
        foreach (var other in OfficialSourceRegistry.LockedLeagueIds.Where(l => l != League))
            Assert.DoesNotContain(OfficialSourceRegistry.KnvbSite,
                OfficialSourceRegistry.VerifiedFor(other, OfficialPurposes.Result).Select(s => s.Key));
        // Kadro/olay/istatistik yeteneği YOK.
        Assert.DoesNotContain(OfficialPurposes.Lineup, knvb.Capabilities);
        Assert.DoesNotContain(OfficialPurposes.Statistics, knvb.Capabilities);
        Assert.Empty(OfficialSourceRegistry.VerifiedFor(League, OfficialPurposes.Lineup));
    }

    [Fact]
    public async Task YanlisOrganizasyonunMaci_KnvbListesindeBulunmaz_SonucYazilmaz()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name))
        {
            db.Teams.AddRange(new Team { Id = 900, Name = "Ajax" }, new Team { Id = 901, Name = "Willem II" });
            // Aynı takım adları, BAŞKA organizasyon (UEFA) → KNVB kaynağı bu lig için kayıtlı değil.
            db.Matches.Add(new Match { Id = 900001, LeagueId = 3, League = "UEFA Europa League",
                MatchDate = new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc), Status = MatchStatuses.NotStarted,
                HomeTeamId = 900, AwayTeamId = 901 });
            db.SaveChanges();
        }
        using (var db = Db(name))
        {
            var report = await Bot(db, new KnvbSiteSource(RealPages())).RunCycleAsync(Now);
            Assert.DoesNotContain(report.SourcesRead, s => s.StartsWith(OfficialSourceRegistry.KnvbSite, StringComparison.Ordinal));
        }
        using (var db = Db(name))
            Assert.Null(db.Matches.Single(m => m.Id == 900001).ResultSource);
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static readonly DateTime Now = new(2026, 9, 17, 19, 0, 0, DateTimeKind.Utc);

    private static OfficialRoundContext Round() => new("test-round", Now, OfficialPurposes.Result);

    private static KnvbSiteSource.KnvbRow Row(DateOnly date, string home, string homeCode, string away, string awayCode, string center)
    {
        int? hs = null, aws = null;
        TimeOnly? kickoff = null;
        var score = center.Split('-');
        if (score.Length == 2 && int.TryParse(score[0], out var h) && int.TryParse(score[1], out var a)) { hs = h; aws = a; }
        else if (TimeOnly.TryParse(center, out var t) && center.Contains(':')) kickoff = t;
        return new KnvbSiteSource.KnvbRow(date, home, away, homeCode, awayCode, center, hs, aws, kickoff);
    }

    /// <summary>Gerçek DB kimlikleriyle (17.09.2026 üretim kaydı) Eredivisie maçları — izole InMemory kopya.</summary>
    private static void Seed(FormaxDbContext db)
    {
        var teams = new (int Id, string Name)[]
        {
            (1, "Ajax"), (2, "Willem II"), (3, "PEC Zwolle"), (4, "Feyenoord"), (5, "PSV Eindhoven"),
            (6, "Sparta Rotterdam"), (7, "Groningen"), (8, "Utrecht"), (9, "GO Ahead Eagles"), (10, "NEC Nijmegen")
        };
        foreach (var t in teams) db.Teams.Add(new Team { Id = t.Id, Name = t.Name });

        void Match_(int id, DateTime kickoff, int home, int away, string status = MatchStatuses.NotStarted)
            => db.Matches.Add(new Match { Id = id, LeagueId = League, League = "Eredivisie", MatchDate = kickoff,
                Status = status, HomeTeamId = home, AwayTeamId = away });

        Match_(15654, new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc), 1, 2);          // Ajax–Willem II 5-1
        Match_(104630, new DateTime(2026, 9, 13, 14, 45, 0, DateTimeKind.Utc), 3, 4);        // PEC Zwolle–Feyenoord 0-7
        Match_(97566, new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc), 5, 6);          // PSV–Sparta 4-1
        Match_(103837, new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc), 8, 9, MatchStatuses.Live); // 05.09 yarıda kaldı, 08.09 tekrar: 3-3
        Match_(104499, new DateTime(2026, 9, 18, 18, 0, 0, DateTimeKind.Utc), 7, 3);         // yaklaşan
        db.SaveChanges();
    }

    private static string SourceFileOf(Type t)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        var file = Directory.EnumerateFiles(dir!.FullName, t.Name + ".cs", SearchOption.AllDirectories)
            .First(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        return File.ReadAllText(file);
    }
}
