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
/// API'SİZ RESMÎ SONUÇ + İSTATİSTİK BOTU (15.09.2026). Gerçek internet YOK: kaynaklar bellek içi kayıt listesi ya da kaydedilmiş
/// gerçek sayfa fikstürüdür; DB InMemory.
/// </summary>
public class OfficialResultBotTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 15, 17, 0, 0, DateTimeKind.Utc);

    private static FormaxDbContext Db(string name) => new(new DbContextOptionsBuilder<FormaxDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name));
    }

    private sealed class CountingSource : IOfficialCompetitionSource
    {
        public string SourceKey { get; init; } = OfficialSourceRegistry.LaLigaSite;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };
        public List<OfficialMatchRecord> Records { get; } = new();
        public int Reads;
        public Func<OfficialRead<IReadOnlyList<OfficialMatchRecord>>>? Override;
        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Reads);
            return Task.FromResult(Override?.Invoke() ?? new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Records.ToList(), OfficialReadOutcomes.Ok, null, null));
        }
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));
    }

    private static OfficialResultBotService Bot(FormaxDbContext db, IOfficialCompetitionSource source)
        => new(db, new[] { source }, new OfficialDataSourceCatalog(db), new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance),
            new ConfigurationBuilder().Build(), NullLogger<OfficialResultBotService>.Instance);

    private static void Seed(FormaxDbContext db, DateTime kickoff, int leagueId = 140)
    {
        db.Teams.AddRange(new Team { Id = 1, Name = "Rayo Vallecano" }, new Team { Id = 2, Name = "Espanyol" });
        db.Matches.Add(new Match { Id = 104380, LeagueId = leagueId, League = "La Liga", MatchDate = kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2, ExternalMatchId = "1570391" });
        db.SaveChanges();
    }

    private static OfficialMatchRecord Rec(string status, int? h, int? a, DateTime? kickoff = null, string home = "Rayo Vallecano", string away = "RCD Espanyol de Barcelona",
        IReadOnlyDictionary<string, string>? extra = null)
        => new(OfficialSourceRegistry.LaLigaSite, "ll-1", "https://www.laliga.com/partido/x", home, away, kickoff ?? Kickoff, status, h, a, status, null, 0, 1, extra);

    // ── 1. TAKVİM ─────────────────────────────────────────────────────────────

    [Fact]
    public void SonucTakvimi_Kickoff105ten165e3dk_180_SonraYarimSaat1_3_6_12_24_SonraGunluk()
    {
        // 17.09.2026 (2): yayın → yazım ≤ 5 dk hedefi için +105…+165 arası 3 dakikada bir kontrol.
        var k = Kickoff;
        Assert.Equal(k.AddMinutes(105), OfficialResultSchedule.FirstCheck(k));
        var expected = Enumerable.Range(1, 20).Select(i => 105 + 3 * i).Concat(new[] { 180, 210, 240, 360, 540, 900, 1620 }).ToArray();
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(k.AddMinutes(expected[i]), OfficialResultSchedule.NextCheck(k, i + 1, k.AddMinutes(100)));
        for (var i = 1; i < 21; i++)
            Assert.True(OfficialResultSchedule.NextCheck(k, i, k.AddMinutes(100)) - OfficialResultSchedule.NextCheck(k, i - 1, k.AddMinutes(100))
                        <= TimeSpan.FromMinutes(OfficialResultSchedule.FinalWindowCadenceMinutes));
        Assert.Equal(k.AddMinutes(1620).AddDays(1), OfficialResultSchedule.NextCheck(k, expected.Length + 1, k.AddMinutes(1000)));
        // Restart sonrası geçmişte kalmış adımlar üst üste çalışmaz.
        var late = k.AddHours(30);
        Assert.True(OfficialResultSchedule.NextCheck(k, 1, late) > late);
    }

    [Fact]
    public void IstatistikTakvimi_10_30_60dk_3_6_12_24sa_SonraGunluk_SonsuzaKadarDegil()
    {
        var f = Kickoff.AddHours(2);
        var steps = new[] { 30, 60, 180, 360, 720, 1440 };
        for (var i = 0; i < steps.Length; i++)
            Assert.Equal(f.AddMinutes(steps[i]), OfficialStatisticsSchedule.NextCheck(f, i + 1, f));
        Assert.Equal(f.AddMinutes(1440).AddDays(1), OfficialStatisticsSchedule.NextCheck(f, 7, f));
        Assert.Null(OfficialStatisticsSchedule.NextCheck(f, 7 + OfficialStatisticsSchedule.MaxDailyChecks, f));
    }

    // ── 2. DURUM EŞLEMESİ ─────────────────────────────────────────────────────

    [Fact]
    public void DurumPolitikasi_FT_AET_PEN_Ertelenen_Iptal_Yarida_SkorsuzBittiKabulEdilmez()
    {
        Assert.Equal("FT", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Finished, 1, 0)).ResultDetail);
        var aet = OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.FinishedAfterExtraTime, 2, 1));
        Assert.Equal(("Final", "AET"), (aet.Kind, aet.ResultDetail));
        var pen = OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.FinishedAfterPenalties, 1, 1,
            extra: new Dictionary<string, string> { ["penaltyHome"] = "4", ["penaltyAway"] = "3" }));
        Assert.Equal(("PEN", 4, 3), (pen.ResultDetail, pen.PenaltyHome, pen.PenaltyAway));
        Assert.Equal("Postponed", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Postponed, null, null)).MatchStatus);
        Assert.Equal("Cancelled", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Cancelled, null, null)).MatchStatus);
        Assert.Equal("Abandoned", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Abandoned, null, null)).MatchStatus);
        Assert.Equal("NotFinal", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Finished, null, null)).Kind);
        Assert.Equal("NotFinal", OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Live, 1, 0)).Kind);
    }

    [Fact]
    public void EflSonucTuru_UzatmaVePenaltiAyirt_Edilir_YaridaKalmaIptalDegildir()
    {
        Assert.Equal(OfficialMatchStatuses.Finished, EflMultiClubSource.RefineFinal("NormalResult", "FullTime"));
        Assert.Equal(OfficialMatchStatuses.FinishedAfterExtraTime, EflMultiClubSource.RefineFinal("AfterExtraTime", "FullTime"));
        Assert.Equal(OfficialMatchStatuses.FinishedAfterPenalties, EflMultiClubSource.RefineFinal("PenaltyShootout", "FullTime"));
        Assert.Equal(OfficialMatchStatuses.Abandoned, EflMultiClubSource.MapStatus("Abandoned"));
        Assert.Equal(OfficialMatchStatuses.Abandoned, PremierLeagueSdpSource.MapStatus("Abandoned"));
    }

    // ── 3. KAYNAK ÖNCELİĞİ / UZLAŞMA ─────────────────────────────────────────

    private static SourceResultObservation Obs(string key, OfficialSourceTier tier, int h, int a)
        => new(key, tier, OfficialResultStatusPolicy.Decide(Rec(OfficialMatchStatuses.Finished, h, a)));

    [Fact]
    public void Uzlasma_LigKaynagiTekBasinaYeterli_KulupKaynagiIkinciKanitIster_CelisenlerConflict()
    {
        Assert.Equal(ResultConsensusPolicy.Accept, ResultConsensusPolicy.Decide(new[] { Obs("league", OfficialSourceTier.LeagueMatchCentre, 2, 1) }).Outcome);
        // Lig kaynağı kulüp kaynağına üstündür.
        var d = ResultConsensusPolicy.Decide(new[] { Obs("home", OfficialSourceTier.HomeClub, 3, 1), Obs("league", OfficialSourceTier.LeagueMatchCentre, 2, 1) });
        Assert.Equal((ResultConsensusPolicy.Accept, 2), (d.Outcome, d.Accepted!.HomeScore));
        // Lig kaynağı yoksa: tek kulüp yetmez; iki kulüp aynıysa kabul; farklıysa çelişki.
        Assert.Equal(ResultConsensusPolicy.Wait, ResultConsensusPolicy.Decide(new[] { Obs("home", OfficialSourceTier.HomeClub, 2, 1) }).Outcome);
        Assert.Equal(ResultConsensusPolicy.Accept, ResultConsensusPolicy.Decide(new[] { Obs("home", OfficialSourceTier.HomeClub, 2, 1), Obs("away", OfficialSourceTier.AwayClub, 2, 1) }).Outcome);
        Assert.Equal(ResultConsensusPolicy.Conflict, ResultConsensusPolicy.Decide(new[] { Obs("home", OfficialSourceTier.HomeClub, 2, 1), Obs("away", OfficialSourceTier.AwayClub, 1, 1) }).Outcome);
        // Aynı öncelikte iki yüksek kaynak farklıysa kanonik yazılmaz.
        Assert.Equal(ResultConsensusPolicy.Conflict, ResultConsensusPolicy.Decide(new[] { Obs("a", OfficialSourceTier.LeagueMatchCentre, 2, 1), Obs("b", OfficialSourceTier.LeagueMatchCentre, 2, 2) }).Outcome);
        // Yayıncı tek başına sonuç yazdırmaz.
        Assert.Equal(ResultConsensusPolicy.Wait, ResultConsensusPolicy.Decide(new[] { Obs("tv", OfficialSourceTier.Broadcaster, 2, 1) }).Outcome);
    }

    // ── 4. BOT ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bot_Kickoff105tenOnceIstekYok_SonraFTYazar_UpcomingdenCikar_AyniGunSonuclarda()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db, Kickoff);
        var src = new CountingSource();
        src.Records.Add(Rec(OfficialMatchStatuses.Finished, 2, 1));

        using (var db = Db(name))
        {
            var r = await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(60));
            Assert.Equal(1, r.Enqueued);
            Assert.Equal(0, src.Reads);                                            // maç sırasında canlı veri aranmaz
            Assert.Equal(Kickoff.AddMinutes(105), db.MatchResultChecks.Single().NextCheckUtc);
        }
        using (var db = Db(name))
        {
            var r = await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
            Assert.Equal(1, r.Applied);
            Assert.Equal(1, src.Reads);
        }
        using (var db = Db(name))
        {
            var m = db.Matches.AsNoTracking().Single();
            Assert.Equal((MatchStatuses.Finished, 2, 1, "FT", "official:laliga-site"), (m.Status, m.HomeScore, m.AwayScore, m.ResultDetail, m.ResultSource));
            Assert.Equal((0, 1), (m.HalfTimeHomeScore, m.HalfTimeAwayScore));
            Assert.False(db.Matches.Any(x => x.Status == MatchStatuses.NotStarted));   // Yaklaşan listesinden çıktı
            var check = db.MatchResultChecks.AsNoTracking().Single();
            Assert.Equal("Resolved", check.State);
            Assert.NotNull(check.FirstFinalSeenUtc);
            // Türkiye günü: 15.09 20:00 TR başlama → aynı gün Sonuçlar'da.
            var results = await TestReaders.Results(db).GetResultsAsync(new DateOnly(2026, 9, 15));
            Assert.Equal("FT", Assert.Single(results).ResultDetail);
            Assert.Single(db.MatchStatisticsChecks);                                   // istatistik işi tetiklendi
        }
    }

    [Fact]
    public async Task Bot_IstanbulGunSiniri_GeceYarisiSonrasiBaslayanMacErtesiGunde()
    {
        var name = Guid.NewGuid().ToString();
        var lateKickoff = new DateTime(2026, 9, 15, 21, 30, 0, DateTimeKind.Utc);            // TR 16.09 00:30
        using (var db = Db(name)) Seed(db, lateKickoff);
        var src = new CountingSource();
        src.Records.Add(Rec(OfficialMatchStatuses.Finished, 0, 0, lateKickoff));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(lateKickoff.AddMinutes(120));
        using (var db = Db(name))
        {
            Assert.Empty(await TestReaders.Results(db).GetResultsAsync(new DateOnly(2026, 9, 15)));
            Assert.Single(await TestReaders.Results(db).GetResultsAsync(new DateOnly(2026, 9, 16)));
        }
    }

    [Fact]
    public async Task KaynakSonradanDogrulandi_KaynakYokKontrolu24SaatBeklemeden_YenidenAcilir_SonucYazilir()
    {
        var name = Guid.NewGuid().ToString();
        var now = Kickoff.AddHours(20);
        using (var db = Db(name))
        {
            Seed(db, Kickoff, leagueId: 3);
            db.MatchResultChecks.Add(new MatchResultCheck { MatchId = 104380, LeagueId = 3, KickoffUtc = Kickoff, State = "NoOfficialSource", AttemptCount = 1,
                NextCheckUtc = now.AddHours(20), LastOutcome = "NoVerifiedOfficialSource", CreatedAtUtc = Kickoff, UpdatedAtUtc = Kickoff });
            db.SaveChanges();
        }
        var src = new CountingSource { SourceKey = OfficialSourceRegistry.UefaMatchApi };
        src.Records.Add(new OfficialMatchRecord(OfficialSourceRegistry.UefaMatchApi, "2050063", null, "Rayo Vallecano", "Espanyol", Kickoff,
            OfficialMatchStatuses.Finished, 1, 0, "FINISHED", null, null, null, new Dictionary<string, string> { ["sourcePublishedAtUtc"] = "2026-09-15T18:52:00Z" }));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(now);
        using (var db = Db(name))
        {
            var m = db.Matches.Single();
            Assert.Equal((MatchStatuses.Finished, 1, 0), (m.Status, m.HomeScore, m.AwayScore));
            Assert.Equal("official:" + OfficialSourceRegistry.UefaMatchApi, m.ResultSource);
            var c = db.MatchResultChecks.Single();
            Assert.Equal("Resolved", c.State);
            Assert.Equal(new DateTime(2026, 9, 15, 18, 52, 0, DateTimeKind.Utc), c.SourcePublishedFinalAtUtc);
        }
    }

    [Fact]
    public async Task Bot_HenuzBitmediyse_TakvimeGoreYenidenPlanlar_RestartSonrasiDevamEder()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db, Kickoff);
        var src = new CountingSource();
        src.Records.Add(Rec(OfficialMatchStatuses.Live, null, null));
        using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
        using (var db = Db(name))
        {
            var c = db.MatchResultChecks.AsNoTracking().Single();
            Assert.Equal(("Pending", 1, Kickoff.AddMinutes(108)), (c.State, c.AttemptCount, c.NextCheckUtc));
            Assert.Equal(Kickoff.AddMinutes(106), c.LastNotFinalCheckUtc);   // gecikme ölçümünün alt sınırı
            Assert.Null(c.LockOwner);
            Assert.Equal(MatchStatuses.NotStarted, db.Matches.Single().Status);
        }
        // "Restart": yeni DbContext + yeni bot örneği; plan DB'den devam eder.
        src.Records.Clear();
        src.Records.Add(Rec(OfficialMatchStatuses.Finished, 1, 1));
        using (var db = Db(name)) Assert.Equal(1, (await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(116))).Applied);
    }

    [Fact]
    public async Task Bot_AyniMacIkiIsciyeVerilmez_KilitSuresiDolunca_Devralinir()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db, Kickoff);
        var now = Kickoff.AddMinutes(110);
        using (var db1 = Db(name))
        using (var db2 = Db(name))
        {
            var a = Bot(db1, new CountingSource());
            var b = Bot(db2, new CountingSource());
            await a.EnqueueAsync(now);
            var first = await a.ClaimDueAsync(now);
            var second = await b.ClaimDueAsync(now);
            Assert.Single(first);
            Assert.Empty(second);                                                    // duplicate iş yok
            var afterExpiry = await b.ClaimDueAsync(now + OfficialResultBotService.LockDuration + TimeSpan.FromSeconds(1));
            Assert.Single(afterExpiry);                                              // çöken işçinin kilidi devralınır
        }
    }

    [Fact]
    public async Task Bot_YanlisTarih_YanlisTakim_TersYon_Reddedilir_SonucYazilmaz()
    {
        foreach (var (rec, expected) in new[]
        {
            (Rec(OfficialMatchStatuses.Finished, 2, 1, Kickoff.AddDays(7)), "WrongDate"),
            (Rec(OfficialMatchStatuses.Finished, 2, 1, home: "Getafe", away: "Sevilla"), "NoCandidate"),
            (Rec(OfficialMatchStatuses.Finished, 2, 1, home: "RCD Espanyol de Barcelona", away: "Rayo Vallecano"), "OrientationMismatch")
        })
        {
            var name = Guid.NewGuid().ToString();
            using (var db = Db(name)) Seed(db, Kickoff);
            var src = new CountingSource();
            src.Records.Add(rec);
            ResultBotCycleReport report;
            using (var db = Db(name)) report = await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
            Assert.Contains(expected, report.Matches.Single().Detail);
            using var check = Db(name);
            Assert.Equal(MatchStatuses.NotStarted, check.Matches.Single().Status);
        }
    }

    [Fact]
    public async Task Bot_ErtelenenVeIptalEdilen_DurumYazilir_ErtelenenGunlukIzlenir()
    {
        foreach (var (status, expected) in new[] { (OfficialMatchStatuses.Postponed, "Postponed"), (OfficialMatchStatuses.Cancelled, "Cancelled"), (OfficialMatchStatuses.Abandoned, "Abandoned") })
        {
            var name = Guid.NewGuid().ToString();
            using (var db = Db(name)) Seed(db, Kickoff);
            var src = new CountingSource();
            src.Records.Add(Rec(status, null, null));
            using (var db = Db(name)) await Bot(db, src).RunCycleAsync(Kickoff.AddMinutes(106));
            using var check = Db(name);
            Assert.Equal(expected, check.Matches.Single().Status);
            var row = check.MatchResultChecks.Single();
            Assert.Equal(expected, row.State);
            if (expected == "Postponed") Assert.Equal(Kickoff.AddMinutes(106).AddHours(24), row.NextCheckUtc);
        }
    }

    [Fact]
    public async Task Bot_ParserBozulursa_ServisCokmez_SaglikKaydi_BesHatadaDevreKesici_RobotsGorunur()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = Db(name)) Seed(db, Kickoff);
        var src = new CountingSource { Override = () => throw new FormatException("beklenmeyen biçim") };
        var now = Kickoff.AddMinutes(106);
        var lastRun = now;
        for (var i = 0; i < 5; i++)
        {
            using var db = Db(name);
            var report = await Bot(db, src).RunCycleAsync(now);
            Assert.Contains(report.Matches, m => m.Outcome == "NoObservation");
            lastRun = now;
            now = db.MatchResultChecks.AsNoTracking().Single().NextCheckUtc;
        }
        now = lastRun.AddMinutes(2);
        using (var db = Db(name))
        {
            var health = db.OfficialDataSources.Single(s => s.SourceId == OfficialSourceRegistry.LaLigaSite);
            Assert.Equal(5, health.ConsecutiveFailureCount);
            Assert.NotNull(health.CircuitBreakerUntilUtc);
            Assert.Contains("FormatException", health.LastError);
            Assert.True(OfficialDataSourceCatalog.IsCircuitOpen(health, now));
            db.MatchResultChecks.Single().NextCheckUtc = now.AddMinutes(-1);           // plan zamanı gelmiş olsa bile
            db.SaveChanges();
            await Bot(db, src).RunCycleAsync(now);                                  // devre açıkken istek yok
        }
        Assert.Equal(5, src.Reads);

        var row = new OfficialDataSource();
        OfficialDataSourceCatalog.Apply(row, OfficialReadOutcomes.FetchFailed, OfficialFetchOutcomes.RobotsDisallowed, OfficialFetchOutcomes.RobotsDisallowed, now);
        Assert.Equal("Disallowed", row.RobotsStatus);
        OfficialDataSourceCatalog.Apply(row, OfficialReadOutcomes.Ok, null, OfficialFetchOutcomes.Fetched, now);
        Assert.Equal((0, "Allowed"), (row.ConsecutiveFailureCount, row.RobotsStatus));
    }

    [Fact]
    public async Task Bot_KaynakKatalogu_KayitDefterindenEslenir_EredivisieRobotsNedeniyleKapali_VideoKaynagiYok()
    {
        using var db = Db(Guid.NewGuid().ToString());
        await new OfficialDataSourceCatalog(db).EnsureSeededAsync(Kickoff);
        var ere = db.OfficialDataSources.Single(s => s.SourceId == "eredivisie-site");
        Assert.False(ere.IsEnabled);
        Assert.Equal("Disallowed", ere.RobotsStatus);
        // 17.09.2026: Eredivisie sonucu federasyon kaynağından (knvb-site) alınır; lig sitesi kapalı kalır.
        var knvb = db.OfficialDataSources.Single(s => s.SourceId == OfficialSourceRegistry.KnvbSite);
        Assert.True(knvb.IsEnabled);
        Assert.Equal("knvb-timetable-v1", knvb.ParserVersion);
        Assert.Equal(OfficialSourceTier.Federation.ToString(), knvb.SourceType);
        Assert.DoesNotContain(db.OfficialDataSources, s => s.SourceId == "youtube-official-channels" || s.SourceId == "trtspor");
        Assert.True(db.OfficialDataSources.Single(s => s.SourceId == OfficialSourceRegistry.LaLigaSite).IsEnabled);
        Assert.Equal("laliga-nextdata-v2", db.OfficialDataSources.Single(s => s.SourceId == OfficialSourceRegistry.LaLigaSite).ParserVersion);
    }

    // ── 5. İSTATİSTİK ─────────────────────────────────────────────────────────

    [Fact]
    public void LaLigaMacSayfasi_GercekAlanlarEslenir_YayimlanmayanAlanNull_YonDogrulanir()
    {
        var html = Fixture("laliga_partido_stats.html");
        var (outcome, stats) = LaLigaSiteSource.ParseMatchStatistics(html, "Real Sociedad", "Celta Vigo");
        Assert.Equal("Ok", outcome);
        Assert.Equal((51, 16, 5, 3, 8, 11, 15, 2), (stats!.Home.BallPossession, stats.Home.TotalShots, stats.Home.ShotsOnTarget, stats.Home.ShotsOffTarget,
            stats.Home.BlockedShots, stats.Home.Corners, stats.Home.Fouls, stats.Home.YellowCards));
        Assert.Equal((526, 452), (stats.Home.TotalPasses, stats.Home.AccuratePasses));           // AccuratePasses = accurate_pass
        Assert.Equal((5, 522), (stats.Away.GoalkeeperSaves, stats.Away.TotalPasses));
        Assert.Null(stats.Home.RedCards);                                                        // kaynak yayımlamadı → null, 0 değil
        Assert.Null(stats.Home.PassAccuracy);                                                    // hesaplanmaz
        Assert.Equal(StatisticsCompleteness.Full, StatisticsCompleteness.Of(stats));
        Assert.Equal("OrientationMismatch", LaLigaSiteSource.ParseMatchStatistics(html, "Celta Vigo", "Real Sociedad").Outcome);
    }

    private static OfficialStatisticsBotService StatsBot(FormaxDbContext db, IOfficialCompetitionSource source)
        => new(db, new[] { source }, new OfficialDataSourceCatalog(db), new ConfigurationBuilder().Build(), NullLogger<OfficialStatisticsBotService>.Instance);

    private sealed class StatsSource : IOfficialCompetitionSource, IOfficialPostMatchSource
    {
        public string SourceKey { get; init; } = OfficialSourceRegistry.LaLigaSite;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Result, OfficialPurposes.Statistics };
        public OfficialMatchStatistics? Stats;
        public int Reads;
        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OfficialRead<IReadOnlyList<OfficialMatchEvent>>> ReadEventsAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<OfficialRead<OfficialMatchStatistics>> ReadStatisticsAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(new OfficialRead<OfficialMatchStatistics>(Stats, OfficialReadOutcomes.Ok, null, null));
        }
    }

    private static OfficialTeamStatistics T(int? possession, int? shots, int? red = null) => new(possession, shots, 2, 3, 1, 4, 1, 10, 2, red, 3, 400, 350, null);

    private static void SeedFinished(FormaxDbContext db, DateTime final)
    {
        Seed(db, Kickoff);
        var m = db.Matches.Single();
        m.Status = MatchStatuses.Finished; m.HomeScore = 2; m.AwayScore = 1; m.ResultSource = "official:laliga-site"; m.ResultUpdatedAtUtc = final;
        db.OfficialMatchLinks.Add(new OfficialMatchLink { MatchId = m.Id, SourceKey = OfficialSourceRegistry.LaLigaSite, OfficialMatchId = "ll-1",
            OfficialUrl = "https://www.laliga.com/partido/x", OfficialHomeName = "Rayo Vallecano", OfficialAwayName = "RCD Espanyol de Barcelona", LinkedAtUtc = final });
        db.MatchStatisticsChecks.Add(new MatchStatisticsCheck { MatchId = m.Id, LeagueId = 140, FinalResultAtUtc = final, NextCheckUtc = final.AddMinutes(10), CreatedAtUtc = final, UpdatedAtUtc = final });
        db.SaveChanges();
    }

    [Fact]
    public async Task IstatistikBotu_KismiVeriKabul_GercekSifirIleNullAyrilir_TekrarYazmaz_TamOluncaKapanir()
    {
        var name = Guid.NewGuid().ToString();
        var final = Kickoff.AddMinutes(112);
        using (var db = Db(name)) SeedFinished(db, final);
        var src = new StatsSource { Stats = new OfficialMatchStatistics(T(55, null, red: 0), T(45, 7)) };

        using (var db = Db(name)) Assert.Empty((await StatsBot(db, src).RunCycleAsync(final.AddMinutes(5))).Matches);   // +10'dan önce yok
        using (var db = Db(name)) await StatsBot(db, src).RunCycleAsync(final.AddMinutes(11));
        using (var db = Db(name))
        {
            var home = db.MatchTeamStatistics.AsNoTracking().Single(s => s.Side == "Home");
            Assert.Null(home.TotalShots);                  // yayımlanmadı
            Assert.Equal(0, home.RedCards);                // açıkça 0
            var check = db.MatchStatisticsChecks.AsNoTracking().Single();
            Assert.Equal(("Pending", StatisticsCompleteness.Partial), (check.State, check.Completeness));
            Assert.Equal(final.AddMinutes(30), check.NextCheckUtc);
            Assert.Equal(2, db.MatchStatisticObservations.Count());
        }
        using (var db = Db(name)) await StatsBot(db, src).RunCycleAsync(final.AddMinutes(31));   // aynı veri → tekrar yazılmaz
        using (var db = Db(name)) Assert.Equal(2, db.MatchStatisticObservations.Count());

        src.Stats = new OfficialMatchStatistics(T(55, 12), T(45, 7));                                // kaynak alanı yayımladı
        using (var db = Db(name)) await StatsBot(db, src).RunCycleAsync(final.AddMinutes(61));
        using (var db = Db(name))
        {
            Assert.Equal(12, db.MatchTeamStatistics.AsNoTracking().Single(s => s.Side == "Home").TotalShots);
            Assert.Equal("Complete", db.MatchStatisticsChecks.AsNoTracking().Single().State);
            Assert.Equal(1, db.MatchTeamStatistics.Count(s => s.Side == "Home"));                  // duplicate yok
        }
    }

    [Fact]
    public async Task IstatistikBotu_FarkliKaynakCelisirse_OncelikliKanonikKalir_CeliskiDeftere()
    {
        var name = Guid.NewGuid().ToString();
        var final = Kickoff.AddMinutes(112);
        using (var db = Db(name))
        {
            SeedFinished(db, final);
            db.MatchTeamStatistics.Add(new MatchTeamStatistic { MatchId = 104380, Side = "Home", BallPossession = 60, Source = "api-football", FetchedAtUtc = final });
            db.SaveChanges();
        }
        var src = new StatsSource { Stats = new OfficialMatchStatistics(T(55, 12), T(45, 7)) };
        using (var db = Db(name)) await StatsBot(db, src).RunCycleAsync(final.AddMinutes(11));
        using (var db = Db(name))
        {
            var home = db.MatchTeamStatistics.AsNoTracking().Single(s => s.Side == "Home");
            Assert.Equal((55, "official:laliga-site"), (home.BallPossession, home.Source));     // resmî lig kaynağı önceliklidir
            var conflict = db.MatchStatisticObservations.AsNoTracking().Single(o => o.Side == "Home");
            Assert.Equal("ConflictRecorded", conflict.Decision);
            Assert.Contains("api-football", conflict.ConflictDetail);
        }
    }

    [Fact]
    public async Task IstatistikBotu_BaglantiYonuTersse_Okumaz()
    {
        var name = Guid.NewGuid().ToString();
        var final = Kickoff.AddMinutes(112);
        using (var db = Db(name))
        {
            SeedFinished(db, final);
            var link = db.OfficialMatchLinks.Single();
            link.OfficialHomeName = "RCD Espanyol de Barcelona"; link.OfficialAwayName = "Rayo Vallecano";
            db.SaveChanges();
        }
        var src = new StatsSource { Stats = new OfficialMatchStatistics(T(55, 12), T(45, 7)) };
        using (var db = Db(name))
        {
            var r = await StatsBot(db, src).RunCycleAsync(final.AddMinutes(11));
            Assert.Contains("OrientationMismatch", r.Matches.Single().Outcome);
        }
        Assert.Equal(0, src.Reads);
        using (var db = Db(name)) Assert.Empty(db.MatchTeamStatistics);
    }

    // ── 6. SAYFA AÇILIŞI / VİDEO KAPALI ───────────────────────────────────────

    private static string Repo(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray()));
    }

    [Fact]
    public void SayfaAcilisi_DisIstekYok_OkumaYollariBotuVeFetcheriCozmez()
    {
        foreach (var file in new[]
        {
            new[] { "Formax.Application", "UseCases", "GetMatchDetailAIContextUseCase.cs" },
            new[] { "Formax.Infrastructure", "Repositories", "MatchResultsReader.cs" },
            new[] { "Formax.API", "Controllers", "MatchOutcomesController.cs" },
            new[] { "Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs" }
        })
        {
            var text = Repo(file);
            var reader = file[^1] == "OutcomeModelServices.cs" ? text[text.IndexOf("class MatchOutcomeSnapshotReader", StringComparison.Ordinal)..] : text;
            foreach (var forbidden in new[] { "IOfficialContentFetcher", "OfficialResultBotService", "OfficialStatisticsBotService", "HttpClient", "ILLMClient", "MatchPredictionSnapshotService" })
                Assert.DoesNotContain(forbidden, reader);
        }
    }

    [Fact]
    public void VideoKapali_IsVeSaglayiciKayitliDegil_DtoVideoTasimaz()
    {
        var program = Repo("Formax.API", "Program.cs");
        foreach (var job in new[] { "MatchVideoDiscoveryJob>", "OfficialVideoSourceCatalogJob>", "OfficialWebFeedCrawlJob>", "MatchVideoRevalidationJob>" })
            Assert.DoesNotContain("AddHostedService<Formax.Infrastructure.BackgroundJobs." + job, program);
        Assert.DoesNotContain("YouTubeRssSocialProvider>", program);
        var di = Repo("Formax.Infrastructure", "DependencyInjection.cs");
        foreach (var svc in new[] { "IOfficialMatchVideoProvider", "IVideoEmbedVerifier", "IMatchVideoReader", "OfficialWebFeedCrawler", "MatchVideoRevalidationService", "postmatch-video" })
            Assert.DoesNotContain(svc, di.Replace("oynatıcı telemetrisi", ""));
        Assert.Null(typeof(Formax.Application.DTOs.Matches.MatchDetailDto).GetProperty("Videos"));
        Assert.Null(typeof(Formax.Application.DTOs.Matches.MatchDetailDto).GetProperty("VideoSearch"));
        Assert.True(Formax.Infrastructure.Telemetry.OutboundHttpObserver.IsVideoRequest("www.youtube.com", "/oembed"));
        Assert.True(Formax.Infrastructure.Telemetry.OutboundHttpObserver.IsVideoRequest("www.trtspor.com.tr", "/sitemap_video.xml"));
        Assert.False(Formax.Infrastructure.Telemetry.OutboundHttpObserver.IsVideoRequest("www.laliga.com", "/partido/x"));
    }
}
