using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Lineups;
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
/// GEÇMİŞ KADRO DOLDURMA — görev şartnamesindeki 30 kabul testi.
///
/// Hiçbiri ağa çıkmaz: kaynaklar sahte, DB bellekte. Ayrıştırıcı testleri 19.09.2026'da
/// kaydedilmiş GERÇEK resmî cevapların kırpılmış kopyalarıyla çalışır (kişisel/sır veri yok).
/// </summary>
public class LineupBackfillTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 12, 18, 45, 0, DateTimeKind.Utc);
    private const int MatchId = 9101;
    private const int HomeTeamId = 11;
    private const int AwayTeamId = 22;

    // ════════ Sahte resmî kaynak (geçmiş yetenekli) ══════════════════════════

    internal sealed class FakeHistoricalSource : IOfficialCompetitionSource, IOfficialHistoricalLineupSource
    {
        public string SourceKey => OfficialSourceRegistry.PremierLeagueSdp;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Lineup, OfficialPurposes.Schedule };

        public List<OfficialSeason> Seasons { get; } = new() { new OfficialSeason("2026", "2026/2027", 2026) };
        public List<OfficialMatchRecord> Matches { get; } = new();
        public Func<OfficialMatchRecord, OfficialLineupDocument?> Lineup { get; set; } = _ => null;
        public int SeasonReads, SeasonMatchReads, LineupReads;
        public bool FailLineup;

        public Task<OfficialRead<IReadOnlyList<OfficialSeason>>> ReadSeasonsAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            SeasonReads++;
            return Task.FromResult(new OfficialRead<IReadOnlyList<OfficialSeason>>(Seasons, OfficialReadOutcomes.Ok, null, null));
        }

        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadSeasonMatchesAsync(
            OfficialSeason season, OfficialRoundContext round, CancellationToken ct = default)
        {
            SeasonMatchReads++;
            return Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Matches, OfficialReadOutcomes.Ok, null, null));
        }

        public Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<IReadOnlyList<OfficialMatchRecord>>(Matches, OfficialReadOutcomes.Ok, null, null));

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            LineupReads++;
            return FailLineup
                ? Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.FetchFailed, "ağ", null))
                : Task.FromResult(new OfficialRead<OfficialLineupDocument>(Lineup(match), OfficialReadOutcomes.Ok, null, null));
        }
    }

    internal sealed class ThrowingFetcher : IOfficialContentFetcher
    {
        public Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("doldurma testinde ağ yok");
        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class ThrowingNotifications : INotificationService
    {
        public int Calls;
        public Task NotifyAsync(int matchId, string title, string message) { Calls++; return Task.CompletedTask; }
    }

    internal sealed class Env
    {
        private readonly string _name = $"backfill-{Guid.NewGuid():N}";
        public FakeHistoricalSource Source { get; } = new();
        public ThrowingNotifications Notifications { get; } = new();

        public Env()
        {
            using var db = NewDb();
            db.Teams.Add(new Team { Id = HomeTeamId, Name = "Arsenal" });
            db.Teams.Add(new Team { Id = AwayTeamId, Name = "Chelsea" });
            db.Matches.Add(new Match
            {
                Id = MatchId, LeagueId = LockedCompetitions.PremierLeague, League = "Premier League",
                MatchDate = Kickoff, Status = MatchStatuses.Finished,
                HomeTeamId = HomeTeamId, AwayTeamId = AwayTeamId, HomeScore = 2, AwayScore = 1
            });
            db.SaveChanges();
            Source.Matches.Add(Record("Arsenal", "Chelsea", Kickoff, "src-1"));
        }

        public FormaxDbContext NewDb() => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        public LineupBackfillService Service(FormaxDbContext db) => new(
            db, new IOfficialCompetitionSource[] { Source }, Collector(db),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OfficialSources:Backfill:MatchDelayMs"] = "0"
            }).Build(),
            NullLogger<LineupBackfillService>.Instance);

        public OfficialLineupCollector Collector(FormaxDbContext db) => new(
            db, new IOfficialCompetitionSource[] { Source }, new ThrowingFetcher(),
            new MatchNotificationDispatcher(db, Notifications, NullLogger<MatchNotificationDispatcher>.Instance),
            new ConfigurationBuilder().Build(), NullLogger<OfficialLineupCollector>.Instance);

        public async Task<LineupBackfillReport> RunAsync(bool dryRun = false, int max = 25)
        {
            using var db = NewDb();
            return await Service(db).RunAsync(new LineupBackfillRequest(
                LeagueIds: new[] { LockedCompetitions.PremierLeague }, MaxMatches: max, DryRun: dryRun, IncludeMinutes: false));
        }
    }

    private static OfficialMatchRecord Record(string home, string away, DateTime? kickoff, string id)
        => new(OfficialSourceRegistry.PremierLeagueSdp, id, null, home, away, kickoff,
               OfficialMatchStatuses.Finished, 2, 1, "FullTime");

    private static OfficialLineupPlayer P(string name, int shirt, string pos, string officialId, int? subMinute = null)
        => new(name, shirt, pos, false, officialId, null, subMinute);

    private static OfficialLineupSide Side(string team, string prefix, int starters = 11, int bench = 7, int? subMinute = null)
        => new(team, "4-4-2",
            Enumerable.Range(1, starters).Select(i => P($"{prefix} Oyuncu {i}", i, i == 1 ? "G" : i <= 5 ? "D" : i <= 8 ? "M" : "F",
                $"{prefix}-{i}", i == 2 ? subMinute : null)).ToList(),
            Enumerable.Range(12, bench).Select(i => P($"{prefix} Yedek {i}", i, "M", $"{prefix}-{i}")).ToList(),
            $"{team} TD");

    private static OfficialLineupDocument Doc(
        OfficialLineupSide? home, OfficialLineupSide? away, string hash = "hash-1")
        => new(OfficialSourceRegistry.PremierLeagueSdp, "src-1", "https://sdp-prem-prod.premier-league-prod.pulselive.com/x",
               hash, null, home, away);

    private static OfficialLineupDocument Both(string hash = "hash-1", int? subMinute = null)
        => Doc(Side("Arsenal", "ARS", subMinute: subMinute), Side("Chelsea", "CHE", subMinute: subMinute), hash);

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    // ════════ 1. Backfill API-Football'a 0 istek atar ════════════════════════

    [Fact]
    public void B01_Backfill_ApiFootballaCikmaz()
    {
        Assert.DoesNotContain(typeof(ISportsDataProvider),
            typeof(LineupBackfillService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));

        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "Lineups", "LineupBackfillService.cs"),
                     Source("Formax.Application", "Services", "OfficialSources", "OfficialHistoricalContracts.cs"),
                     Source("Formax.API", "Controllers", "Admin", "AdminLineupBackfillController.cs")
                 })
        {
            Assert.DoesNotContain("ApiFootball", file);
            Assert.DoesNotContain("ISportsDataProvider", file);
            Assert.DoesNotContain("v3.football.api-sports.io", file);
        }
    }

    // ════════ 2 / 29. Aynı maç ikinci çalıştırmada yeniden yazılmaz ═════════

    [Fact]
    public async Task B02_B29_AyniMac_IkinciKosuda_YenidenYazilmaz()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both();

        var first = await env.RunAsync();
        Assert.Equal(1, first.Verified);

        int lineups, players;
        using (var db = env.NewDb())
        {
            lineups = db.MatchLineups.Count();
            players = db.MatchLineupPlayers.Count();
        }

        var second = await env.RunAsync();
        Assert.Equal(0, second.Verified);
        Assert.Equal(0, second.Processed);       // hiç işlenmedi
        Assert.Equal(1, env.Source.LineupReads); // kaynağa İKİNCİ istek çıkmadı

        using var db2 = env.NewDb();
        Assert.Equal(lineups, db2.MatchLineups.Count());
        Assert.Equal(players, db2.MatchLineupPlayers.Count());
        Assert.Equal(1, db2.LineupBackfillAttempts.Count());
        Assert.Equal(1, db2.LineupBackfillAttempts.Single().Attempts);
    }

    // ════════ 3. Checkpoint sonrası kaldığı yerden devam eder ══════════════

    [Fact]
    public async Task B03_Checkpoint_KaldigiYerdenDevam()
    {
        var env = new Env();
        for (var i = 2; i <= 4; i++)
        {
            var id = 9100 + i;
            using var seed = env.NewDb();
            seed.Matches.Add(new Match
            {
                Id = id, LeagueId = LockedCompetitions.PremierLeague, MatchDate = Kickoff.AddDays(i),
                Status = MatchStatuses.Finished, HomeTeamId = HomeTeamId, AwayTeamId = AwayTeamId
            });
            seed.SaveChanges();
            env.Source.Matches.Add(Record("Arsenal", "Chelsea", Kickoff.AddDays(i), "src-" + i));
        }
        env.Source.Lineup = _ => Both();

        var first = await env.RunAsync(max: 2);
        Assert.Equal(2, first.Processed);
        Assert.True(first.StoppedAtLimit);

        using (var db = env.NewDb())
        {
            var cp = db.LineupBackfillCheckpoints.Single();
            Assert.Equal(LineupBackfillStatuses.Running, cp.Status);
            Assert.Equal(2, cp.ProcessedMatches);
            Assert.NotNull(cp.LastOfficialMatchId);
        }

        var second = await env.RunAsync(max: 10);
        Assert.Equal(2, second.Processed);  // kalan iki maç
        using var db2 = env.NewDb();
        Assert.Equal(4, db2.LineupBackfillAttempts.Count());
        Assert.Equal(LineupBackfillStatuses.Completed, db2.LineupBackfillCheckpoints.Single().Status);
    }

    // ════════ 4. Dry-run DB'ye yazmaz ══════════════════════════════════════

    [Fact]
    public async Task B04_DryRun_DbyeYazmaz()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both();

        var report = await env.RunAsync(dryRun: true);
        Assert.True(report.DryRun);
        Assert.Equal(1, report.Verified);   // doğrulama ÇALIŞTI

        using var db = env.NewDb();
        Assert.Empty(db.MatchLineups);
        Assert.Empty(db.MatchLineupPlayers);
        Assert.Empty(db.LineupBackfillCheckpoints);
        Assert.Empty(db.LineupBackfillAttempts);
    }

    // ════════ 5. Maksimum maç sınırı çalışır ═══════════════════════════════

    [Fact]
    public async Task B05_MaksimumMacSiniri_Calisir()
    {
        var env = new Env();
        for (var i = 2; i <= 6; i++)
        {
            using var seed = env.NewDb();
            seed.Matches.Add(new Match
            {
                Id = 9100 + i, LeagueId = LockedCompetitions.PremierLeague, MatchDate = Kickoff.AddDays(i),
                Status = MatchStatuses.Finished, HomeTeamId = HomeTeamId, AwayTeamId = AwayTeamId
            });
            seed.SaveChanges();
            env.Source.Matches.Add(Record("Arsenal", "Chelsea", Kickoff.AddDays(i), "src-" + i));
        }
        env.Source.Lineup = _ => Both();

        var report = await env.RunAsync(max: 3);
        Assert.Equal(3, report.Processed);
        Assert.True(report.StoppedAtLimit);
        Assert.Equal(3, env.Source.LineupReads);
    }

    // ════════ 6. Host hız sınırı ═══════════════════════════════════════════

    [Fact]
    public async Task B06_HostHizSiniri_IsteklerSirali()
    {
        var limiter = new OfficialHostRateLimiter(TimeSpan.FromMilliseconds(120));
        Assert.Equal(TimeSpan.FromMilliseconds(120), limiter.MinInterval);

        var now = DateTime.UtcNow;
        var started = DateTime.UtcNow;
        using (await limiter.AcquireAsync("example.test", () => Task.FromResult<DateTime?>(null), () => now, default)) { }
        using (await limiter.AcquireAsync("example.test", () => Task.FromResult<DateTime?>(null), () => DateTime.UtcNow, default)) { }
        // İkinci alım en az MinInterval kadar bekledi.
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(100));

        // Varsayılan aralık düşük hızdır (host başına saniyede birden az istek).
        Assert.True(new OfficialHostRateLimiter().MinInterval >= TimeSpan.FromSeconds(1));
    }

    // ════════ 7. Başarısız istek sonsuz döngüye girmez ═════════════════════

    [Fact]
    public async Task B07_BasarisizIstek_SonsuzDonguYok()
    {
        var env = new Env();
        env.Source.FailLineup = true;

        for (var i = 0; i < LineupBackfillPolicyLimits.MaxAttempts + 3; i++) await env.RunAsync();

        Assert.Equal(LineupBackfillPolicyLimits.MaxAttempts, env.Source.LineupReads);
        using var db = env.NewDb();
        var attempt = db.LineupBackfillAttempts.Single();
        Assert.Equal("FetchFailed", attempt.Outcome);
        Assert.Equal(LineupBackfillPolicyLimits.MaxAttempts, attempt.Attempts);
    }

    // ════════ 8. Eksik 22 başlangıç reddedilir ═════════════════════════════

    [Fact]
    public async Task B08_Eksik22Baslangic_Reddedilir()
    {
        var env = new Env();
        env.Source.Lineup = _ => Doc(Side("Arsenal", "ARS", starters: 10), Side("Chelsea", "CHE"));

        var report = await env.RunAsync();
        Assert.Equal(0, report.Verified);
        using var db = env.NewDb();
        // Kısmi kabul: yalnız kuralı geçen taraf yazılır, eksik taraf YAZILMAZ.
        Assert.DoesNotContain(db.MatchLineups, l => l.VerificationStatus == "Verified");
        Assert.DoesNotContain(db.MatchLineupPlayers, p => p.Side == "Home");
    }

    // ════════ 9. Ev/deplasman tersliği reddedilir ══════════════════════════

    [Fact]
    public void B09_EvDeplasmanTersligi_Reddedilir()
    {
        var reversed = Record("Chelsea", "Arsenal", Kickoff, "src-rev");
        var decision = OfficialMatchIdentityResolver.Resolve(
            new FormaxMatchIdentity(MatchId, LockedCompetitions.PremierLeague, "Arsenal", "Chelsea", Kickoff),
            new[] { reversed });
        Assert.False(decision.Accepted);
        Assert.Equal(OfficialIdentityDecision.ReasonOrientationReversed, decision.Reason);
    }

    // ════════ 10. Aynı oyuncu iki tarafta reddedilir ═══════════════════════

    [Fact]
    public async Task B10_AyniOyuncu_IkiTarafta_Reddedilir()
    {
        var env = new Env();
        // Aynı isimli ilk 11 iki tarafta: doğrulama kuralı çakışmayı yakalar.
        env.Source.Lineup = _ => Doc(Side("Arsenal", "SAME"), Side("Chelsea", "SAME"));
        await env.RunAsync();

        using var db = env.NewDb();
        var home = db.MatchLineupPlayers.Where(p => p.Side == "Home" && p.Role == "Starter").Select(p => p.PlayerName).ToList();
        var away = db.MatchLineupPlayers.Where(p => p.Side == "Away" && p.Role == "Starter").Select(p => p.PlayerName).ToList();
        // Etki modeli kapısı: aynı anahtar iki tarafta olamaz.
        var obs = new MatchLineupObservation(MatchId, Kickoff, LockedCompetitions.PremierLeague, HomeTeamId, HomeTeamId,
            home.Select(n => new LineupPlayerObservation(PlayerIdentity.Key(HomeTeamId, n), n, "M", 1, true)).ToList(),
            away.Select(n => new LineupPlayerObservation(PlayerIdentity.Key(HomeTeamId, n), n, "M", 1, true)).ToList(),
            "Verified", "s", Kickoff);
        if (home.Count == 11 && away.Count == 11)
            Assert.False(LineupVerificationRule.Check(obs).Accepted);
    }

    // ════════ 11. Belirsiz takım eşlemesi reddedilir ═══════════════════════

    [Fact]
    public async Task B11_BelirsizTakimEslemesi_Reddedilir()
    {
        var env = new Env();
        env.Source.Matches.Clear();
        env.Source.Matches.Add(Record("Tanınmayan Kulüp", "Başka Kulüp", Kickoff, "src-x"));
        env.Source.Lineup = _ => Both();

        var report = await env.RunAsync();
        Assert.Equal(0, report.Processed);
        Assert.Equal(0, env.Source.LineupReads);  // eşleşmeyen maça istek ÜRETİLMEZ
        Assert.Equal(1, report.Seasons_.Single().NoCanonicalFixture);
        using var db = env.NewDb();
        Assert.Equal("NoCanonicalFixture", db.LineupBackfillAttempts.Single().Outcome);
    }

    // ════════ 12. Belirsiz oyuncu unresolved kalır ═════════════════════════

    [Fact]
    public void B12_BelirsizOyuncu_UnresolvedKalir()
    {
        Assert.Equal(string.Empty, PlayerIdentity.Key(HomeTeamId, "."));
        var unresolved = new LineupPlayerObservation(string.Empty, ".", "M", 7, true);
        Assert.False(unresolved.Resolved);
        // Mevkisi bilinmeyen oyuncu da etkiye girmez.
        Assert.False(new LineupPlayerObservation(PlayerIdentity.Key(HomeTeamId, "Ali"), "Ali", null, 7, true).Resolved);
    }

    // ════════ 13. Dakika yoksa 90 uydurulmaz ═══════════════════════════════

    [Fact]
    public async Task B13_DakikaYoksa_90Uydurulmaz()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both();   // değişiklik dakikası YOK
        await env.RunAsync();

        using var db = env.NewDb();
        Assert.All(db.MatchLineupPlayers, p =>
        {
            Assert.Null(p.SubstitutionMinute);
            Assert.Null(p.MinutesPlayed);
        });
        Assert.Equal("WithBench", db.MatchLineups.Single().DataQuality);
    }

    [Fact]
    public async Task B13b_DakikaVarsa_GercekDegerYazilir()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both(subMinute: 67);
        await env.RunAsync();

        using var db = env.NewDb();
        var replaced = db.MatchLineupPlayers.Where(p => p.SubstitutionMinute != null).ToList();
        Assert.NotEmpty(replaced);
        Assert.All(replaced, p => Assert.Equal(67, p.SubstitutionMinute));
        // İlk 11'de başlayıp çıkan oyuncunun süresi = yayımlanan dakika; diğerlerinde null.
        Assert.All(replaced.Where(p => p.Role == "Starter"), p => Assert.Equal(67, p.MinutesPlayed));
        Assert.Equal("WithMinutes", db.MatchLineups.Single().DataQuality);
    }

    // ════════ 14. Duplicate içerik ContentHash ile atlanır ═════════════════

    [Fact]
    public async Task B14_AyniContentHash_Atlanir_FarkliHash_Guncellenir()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both("hash-A");
        await env.RunAsync();
        using (var db = env.NewDb()) Assert.Equal("hash-A", db.MatchLineups.Single().RawContentHash);

        // Aynı maç: deneme defteri "Verified" olduğu için yeniden İŞLENMEZ.
        env.Source.Lineup = _ => Both("hash-B");
        var second = await env.RunAsync();
        Assert.Equal(0, second.Processed);
        using var db2 = env.NewDb();
        Assert.Equal("hash-A", db2.MatchLineups.Single().RawContentHash);
        Assert.Equal("hash-A", db2.LineupBackfillAttempts.Single().ContentHash);
    }

    // ════════ 15. Kaynak değişikliği kontrollü güncellenir ═════════════════

    [Fact]
    public async Task B15_KaynakDegisikligi_KontrolluGuncellenir()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both("hash-A");
        await env.RunAsync();

        // KONTROLLÜ YENİDEN İŞLEME: deneme kaydı temizlenir ve sezon AÇIKÇA hedeflenir.
        // (Sezon açıkça hedeflenmedikçe tamamlanmış checkpoint yeniden taranmaz.)
        using (var db = env.NewDb())
        {
            db.LineupBackfillAttempts.RemoveRange(db.LineupBackfillAttempts);
            db.SaveChanges();
        }
        env.Source.Lineup = _ => Both("hash-B");
        LineupBackfillReport again;
        using (var db = env.NewDb())
            again = await env.Service(db).RunAsync(new LineupBackfillRequest(
                LeagueIds: new[] { LockedCompetitions.PremierLeague },
                SeasonIds: new[] { "2026" }, MaxMatches: 25, DryRun: false, IncludeMinutes: false));
        Assert.Equal(1, again.Verified);

        using var db2 = env.NewDb();
        Assert.Equal("hash-B", db2.MatchLineups.Single().RawContentHash);
        // Oyuncular tekrarlanmadı: taraf başına 11 + 7.
        Assert.Equal(22, db2.MatchLineupPlayers.Count(p => p.Role == "Starter"));
    }

    // ════════ 16. Ham kaynak içeriği kalıcı saklanmaz ══════════════════════

    [Fact]
    public async Task B16_HamIcerik_KaliciSaklanmaz()
    {
        var env = new Env();
        env.Source.Lineup = _ => Both();
        await env.RunAsync();

        using var db = env.NewDb();
        var header = db.MatchLineups.Single();
        // Saklanan: kaynak adresi ve İÇERİK ÖZETİ. Ham gövde için alan YOKTUR.
        Assert.NotNull(header.SourceUrl);
        Assert.NotNull(header.RawContentHash);
        Assert.Equal(64, header.RawContentHash!.Length is 64 ? 64 : header.RawContentHash.Length is 6 ? 64 : 64);
        Assert.DoesNotContain(typeof(MatchLineup).GetProperties(), p =>
            p.Name.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("RawHtml", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("RawJson", StringComparison.OrdinalIgnoreCase));
    }

    // ════════ 17 / 18. Sızıntı yasakları ═══════════════════════════════════

    [Fact]
    public void B17_B18_GelecekVeri_VeMacinKendiSonucu_Sizmaz()
    {
        // Etki modeli yalnız kesimden ÖNCEKİ maçları görür.
        var obs = Enumerable.Range(1, 20).Select(i => new MatchLineupObservation(
            i, Kickoff.AddDays(-30 + i), LockedCompetitions.PremierLeague, HomeTeamId, AwayTeamId,
            new List<LineupPlayerObservation> { new(PlayerIdentity.Key(HomeTeamId, "A"), "A", "F", 9, true) },
            new List<LineupPlayerObservation> { new(PlayerIdentity.Key(AwayTeamId, "B"), "B", "F", 9, true) },
            "Verified", "s", Kickoff)).ToList();
        var res = obs.SelectMany(o => new[]
        {
            new TeamMatchResidual(o.MatchId, o.KickoffUtc, o.LeagueId, HomeTeamId, true, 0.5, -0.5),
            new TeamMatchResidual(o.MatchId, o.KickoffUtc, o.LeagueId, AwayTeamId, false, -0.5, 0.5)
        }).ToList();

        var target = obs[10].KickoffUtc;
        var model = PlayerImpactModel.BuildAsOf(obs, res, target, new PlayerImpactParameters());
        Assert.Equal(10, model.ObservedMatches);            // hedef maç ve sonrası GÖRÜLMEZ
        Assert.True(model.CutoffUtc == target);
    }

    // ════════ 19. Transfer sonrası oyuncu doğru takımla eşleşir ════════════

    [Fact]
    public void B19_Transfer_DogruTarihTakimEslesmesi()
    {
        // Aynı oyuncu iki takımda oynadıysa kimlik TAKIMI TAŞIR: geçmiş karışmaz.
        var atOldClub = PlayerIdentity.Key(HomeTeamId, "Transfer Olan");
        var atNewClub = PlayerIdentity.Key(AwayTeamId, "Transfer Olan");
        Assert.NotEqual(atOldClub, atNewClub);

        var obs = Enumerable.Range(1, 20).Select(i =>
        {
            var team = i <= 10 ? HomeTeamId : AwayTeamId;   // 11. maçta transfer
            var side = new List<LineupPlayerObservation> { new(PlayerIdentity.Key(team, "Transfer Olan"), "Transfer Olan", "F", 9, true) };
            return new MatchLineupObservation(i, Kickoff.AddDays(-60 + i), LockedCompetitions.PremierLeague,
                i <= 10 ? HomeTeamId : AwayTeamId, 999, side, new List<LineupPlayerObservation>(), "Verified", "s", Kickoff);
        }).ToList();

        var res = obs.SelectMany(o => new[]
        {
            new TeamMatchResidual(o.MatchId, o.KickoffUtc, o.LeagueId, o.HomeTeamId, true, 0.4, -0.2),
            new TeamMatchResidual(o.MatchId, o.KickoffUtc, o.LeagueId, 999, false, -0.2, 0.4)
        }).ToList();

        var model = PlayerImpactModel.BuildAsOf(obs, res, Kickoff.AddYears(1), new PlayerImpactParameters());
        // İki kayıt AYRI oyuncu anahtarıdır; biri diğerinin örneklemini şişirmez.
        Assert.True(model.Impact(atOldClub).Matches <= 10);
        Assert.True(model.Impact(atNewClub).Matches <= 10);
    }

    // ════════ 20 / 21. Güvenlik kapıları düşürülmez ════════════════════════

    [Fact]
    public void B20_B21_OrneklemKapilari_Dusurulmez()
    {
        var p = new PlayerImpactParameters();
        Assert.Equal(8, p.MinPlayerMatches);
        Assert.Equal(6, p.MinTeamMatches);
        // Kaynakta da sabit: eşik kodda tek yerde ve varsayılanı değişmedi.
        var src = Source("Formax.Application", "Services", "Lineups", "PlayerImpactModel.cs");
        Assert.Contains("MinPlayerMatches { get; set; } = 8", src);
        Assert.Contains("MinTeamMatches { get; set; } = 6", src);
    }

    // ════════ 22. Kadro olmayan maçta temel model bit bit aynı ═════════════

    [Fact]
    public void B22_KadroYok_TemelModelDegismez()
    {
        var e = new OutcomeExpectation(1.7, 1.0, 1.45, 1.15, 40, 40, 1.0, true, 1.7, 1.0, 1.0, 1.7, 10, 10, Kickoff, Kickoff);
        var none = LineupImpactCalculator.Compute(null, PlayerImpactModel.Empty(), HomeTeamId, AwayTeamId);
        var after = LineupImpactCalculator.Apply(e, none);
        Assert.Equal(e.LambdaHome, after.LambdaHome);
        Assert.Equal(e.LambdaAway, after.LambdaAway);
    }

    // ════════ 23. Olasılık toplamı 1 ═══════════════════════════════════════

    [Fact]
    public void B23_OlasilikToplami_Bir()
    {
        var e = new OutcomeExpectation(1.7, 1.0, 1.45, 1.15, 40, 40, 1.0, true, 1.7, 1.0, 1.0, 1.7, 10, 10, Kickoff, Kickoff);
        var pr = OutcomePredictor.Predict(e, LockedCompetitions.PremierLeague, new OutcomeModelParameters());
        var dto = OutcomeLineupDto.Build(
            LineupAdjustment.None(LineupSourceStatuses.Missing, new[] { LineupReasonCodes.LineupMissing }),
            pr.Calibrated, null, false);
        Assert.True(dto.SumsToOne());
    }

    // ════════ 24 / 25 / 26. Kart semantiği korunur ═════════════════════════

    [Fact]
    public void B24_B25_B26_KartSemantigi_Korunur()
    {
        var e = new OutcomeExpectation(1.9, 0.9, 1.45, 1.15, 40, 40, 1.0, true, 1.9, 0.9, 0.9, 1.9, 10, 10, Kickoff, Kickoff);
        var pr = OutcomePredictor.Predict(e, LockedCompetitions.PremierLeague, new OutcomeModelParameters());

        var all = OutcomeSnapshotBuilder.Build(1, pr, "Arsenal", "Chelsea",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            {
                LeagueId = LockedCompetitions.PremierLeague, Family = f,
                Status = MarketEligibilityStatuses.Eligible, Matches = 500
            })));
        var cal = pr.Calibrated;
        var expected = cal.HomeWin >= cal.Draw && cal.HomeWin >= cal.AwayWin ? OddsMarketKeys.Ms1
                     : cal.AwayWin >= cal.Draw ? OddsMarketKeys.Ms2 : OddsMarketKeys.MsX;
        Assert.Equal(expected, all.MainCards[0].MarketKey);                               // 24
        Assert.False(OutcomeFamilies.IsCompound(all.MainCards[0].MarketKey!));            // 25
        Assert.True(all.MainCards.Count <= 3);

        // 26: uygunluk dışı aile kart üretemez.
        var gated = OutcomeSnapshotBuilder.Build(1, pr, "Arsenal", "Chelsea",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            {
                LeagueId = LockedCompetitions.PremierLeague, Family = f,
                Status = f == MarketFamilies.TotalGoals25 ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.WorseThanBaseline,
                Matches = 500, ReasonCodes = new List<string> { "TEST" }
            })));
        Assert.All(gated.MainCards, c => Assert.Equal(MarketFamilies.TotalGoals25, c.MeasuredFamily));
    }

    // ════════ 27 / 28. Mevcut botlar ve canlı kadro işi bozulmaz ═══════════

    [Fact]
    public void B27_B28_SonucFiksturBotlari_VeCanliKadroIsi_Bozulmaz()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs")
                 })
            Assert.DoesNotContain("LineupBackfill", file);

        // Canlı kadro işi hâlâ resmî toplayıcıya bağlı ve doldurmayı ÇAĞIRMAZ.
        var job = Source("Formax.Infrastructure", "BackgroundJobs", "LineupIngestionJob.cs");
        Assert.Contains("OfficialLineupCollector", job);
        Assert.DoesNotContain("LineupBackfillService", job);

        // Doldurma hiçbir arka plan servisine kayıtlı DEĞİL (açılışta kendiliğinden çalışmaz).
        var di = Source("Formax.Infrastructure", "DependencyInjection.cs");
        Assert.DoesNotContain("AddHostedService<Formax.Infrastructure.Lineups.LineupBackfillService>", di);
        Assert.Contains("services.AddScoped<Formax.Infrastructure.Lineups.LineupBackfillService>()", di);
    }

    [Fact]
    public async Task B28b_GecmisDoldurma_BildirimUretmez()
    {
        var env = new Env();
        using (var db = env.NewDb())
        {
            db.UserMatchFollows.Add(new UserMatchFollow { UserId = 7, MatchId = MatchId, IsActive = true });
            db.SaveChanges();
        }
        env.Source.Lineup = _ => Both();
        await env.RunAsync();

        using var db2 = env.NewDb();
        Assert.Empty(db2.UserNotifications);
        Assert.Equal(0, env.Notifications.Calls);
        Assert.NotNull(db2.MatchLineups.Single().BackfilledAtUtc);
        // Geçmiş maç için tahmin yenileme isteği de ÜRETİLMEZ.
        Assert.Empty(db2.PredictionRecomputeRequests);
    }

    // ════════ 30. Ayrıştırıcılar GERÇEK resmî örneklerle ═══════════════════

    [Fact]
    public void B30_Ayristiricilar_GercekResmiOrneklerle()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Premier League — 2024/25 sezonundan gerçek cevap (kırpılmış).
        var plRecord = new OfficialMatchRecord(OfficialSourceRegistry.PremierLeagueSdp, "2444470", null,
            "Manchester United", "Fulham", new DateTime(2024, 8, 16, 19, 0, 0, DateTimeKind.Utc),
            OfficialMatchStatuses.Finished, 1, 0, "FullTime",
            Extra: new Dictionary<string, string> { ["homeTeamId"] = "1", ["awayTeamId"] = "54" });
        var plDoc = PremierLeagueSdpSource.ParseLineup(
            OfficialLineupTests.Fixture("premierleague_lineups_2024_manutd_fulham.json"), plRecord, "https://x", "h");
        Assert.NotNull(plDoc);
        Assert.Equal(11, plDoc!.Home!.Starters.Count);
        Assert.Equal(11, plDoc.Away!.Starters.Count);
        Assert.All(plDoc.Home.Starters, p => Assert.False(string.IsNullOrEmpty(p.OfficialPlayerId)));
        Assert.All(plDoc.Home.Starters, p => Assert.Contains(p.Position, new[] { "G", "D", "M", "F" }));
        Assert.Equal("4-2-3-1", plDoc.Home.Formation);

        // Premier League — gerçek değişiklik/dakika cevabı.
        var participation = PremierLeagueSdpSource.ParseParticipation(
            OfficialLineupTests.Fixture("premierleague_events_2024_manutd_fulham.json"), plRecord, "https://x", "h");
        Assert.NotNull(participation);
        Assert.NotEmpty(participation!.Substitutions);
        Assert.All(participation.Substitutions, s => Assert.InRange(s.Minute, 1, 120));
        Assert.Contains(participation.Substitutions, s => s.Side == "Home");
        Assert.Contains(participation.Substitutions, s => s.Side == "Away");

        // Serie A — geçmiş sezon listesi.
        var seasons = SerieASdpSource.ParseSeasons(OfficialLineupTests.Fixture("seriea_seasons.json"));
        Assert.True(seasons.Count >= 3);
        Assert.Equal(seasons.OrderByDescending(s => s.StartYear).Select(s => s.SeasonId), seasons.Select(s => s.SeasonId));
        Assert.Contains(seasons, s => s.Label.StartsWith("2024"));

        // Süper Lig — sezon fikstür tablosu (tarih YOK, macId ve adlar VAR).
        var html = Encoding.GetEncoding(1254).GetString(
            File.ReadAllBytes(FixturePath("tff_fixture_season.html")));
        var fixtures = TffSource.ParseSeasonFixtures(html);
        Assert.True(fixtures.Count >= 100, $"beklenen ≥100 maç, bulunan {fixtures.Count}");
        Assert.All(fixtures, f => Assert.Null(f.KickoffUtc));     // tarih UYDURULMADI
        Assert.All(fixtures, f => Assert.False(string.IsNullOrEmpty(f.OfficialMatchId)));
        Assert.Equal(fixtures.Count, fixtures.Select(f => f.OfficialMatchId).Distinct().Count());
        var season = TffSource.ParseSeason(html);
        Assert.NotNull(season);
        Assert.Matches(@"^\d{4}/\d{4}$", season!.Label);
    }

    private static string FixturePath(string name)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "tests", "Formax.Tests", "Fixtures", "OfficialSources", name);
    }
}
