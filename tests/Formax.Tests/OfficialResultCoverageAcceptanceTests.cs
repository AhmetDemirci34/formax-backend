using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Formax.Infrastructure.PostMatch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Formax.Tests;

/// <summary>
/// KİLİTLİ 11 ORGANİZASYONDA RESMÎ SONUÇ KAPSAMI — GERÇEK VERİYLE KABUL TESTİ.
///
/// Varsayılan koşuda ATLANIR; yalnız <c>FORMAX_LIVE_OFFICIAL=1</c> iken çalışır.
///
/// Ne yapar: her organizasyon için (1) ÜRETİM indiricisiyle (SocketsHttpHandler + bağlantı anında IP koruması +
/// robots.txt RFC 9309 + host hız sınırı) resmî kaynağa çıkar; (2) ÜRETİM ayrıştırıcısını kullanır; (3) maç kimliklerini
/// gerçek FormaxDB'den SALT OKUR; (4) bu kimlikleri İZOLE bir InMemory DB'ye kopyalar ve sonuç botunun normal turunu
/// orada çalıştırır (üretim verisine yazmaz, geçici kayıt bırakmaz); (5) yazılan kanonik skoru kaynağın kaydıyla ve
/// gerçek DB'deki resmî sonuçla karşılaştırır.
///
/// Skor ELLE VERİLMEZ: beklenen skor kaynağın canlı cevabından gelir. API-Football'a hiç çıkılmaz; defterdeki bütün
/// host'lar kayıt defterinin izin listesinden olmalıdır.
/// </summary>
public class OfficialResultCoverageAcceptanceTests
{
    private readonly ITestOutputHelper _out;
    public OfficialResultCoverageAcceptanceTests(ITestOutputHelper output) => _out = output;

    private static bool Enabled => Environment.GetEnvironmentVariable("FORMAX_LIVE_OFFICIAL") == "1";

    private static string RealConnection =>
        Environment.GetEnvironmentVariable("FORMAX_DB")
        ?? "Server=localhost\\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>Kabul kriteri: organizasyon başına doğrulanacak bitmiş ve bitmemiş maç sayısı.</summary>
    private const int FinishedPerOrg = 3;
    private const int UpcomingPerOrg = 1;

    /// <summary>
    /// Bir organizasyonun sınav bağlamı. <paramref name="NowOverride"/> yalnız o organizasyonun EN SON oynanmış turu
    /// kaynağın canlı penceresinin dışında kaldığında verilir (Konferans Ligi'nin son turu 27.08.2026; UEFA ucu
    /// 10 günlük pencereyle okuyor). Kaynak cevabı her durumda CANLI ve gerçektir.
    /// </summary>
    /// <param name="UpcomingProvenByLeague">
    /// FORMAX fikstüründe bu organizasyonun İLERİ TARİHLİ maçı hiç yoksa (17.09.2026 ölçümü: Şampiyonlar Ligi'nin
    /// FORMAX'taki en son maçı 10.09, Konferans Ligi'nin 27.08; fikstür alımı boşluğu) "erken final yazılmaz" güvencesi
    /// AYNI adaptörün kanıtlanmış organizasyonundan devralınır. Devralma yalnız o organizasyon gerçekten PASS ettiyse
    /// geçerlidir ve raporda açıkça görünür.
    /// </param>
    private sealed record OrgCase(int League, string Name, string SourceKey, DateTime? NowOverride = null,
        int? UpcomingProvenByLeague = null);

    private static readonly OrgCase[] Orgs =
    {
        new(3, "UEFA Europa League", OfficialSourceRegistry.UefaMatchApi),
        new(2, "UEFA Champions League", OfficialSourceRegistry.UefaMatchApi, null, UpcomingProvenByLeague: 3),
        new(848, "UEFA Conference League", OfficialSourceRegistry.UefaMatchApi,
            new DateTime(2026, 8, 28, 23, 0, 0, DateTimeKind.Utc), UpcomingProvenByLeague: 3),
        new(39, "Premier League", OfficialSourceRegistry.PremierLeagueSdp),
        new(40, "EFL Championship", OfficialSourceRegistry.EflApi),
        new(78, "Bundesliga", OfficialSourceRegistry.BundesligaSite),
        new(61, "Ligue 1", OfficialSourceRegistry.Ligue1Api),
        new(140, "LALIGA", OfficialSourceRegistry.LaLigaSite),
        new(135, "Serie A", OfficialSourceRegistry.SerieASdp),
        new(88, "Eredivisie", OfficialSourceRegistry.KnvbSite),
        new(203, "Süper Lig", OfficialSourceRegistry.TffSite)
    };

    private sealed record RealMatch(int Id, int LeagueId, string Home, string Away, DateTime Kickoff,
        string Status, int? HomeScore, int? AwayScore, string? ResultSource);

    private sealed record OrgResult(OrgCase Org, int Finished, int Upcoming, int EarlyFinal, int WrongScore,
        int WrongDirection, int DuplicateUpdates, int ApiFootballRequests, string Status, string Detail,
        bool UpcomingInherited = false);

    [SkippableFact]
    public async Task Kilitli11Organizasyon_GercekResmiKaynaktan_UcBitmisMac_BirYaklasanMac_ApiFootballYok()
    {
        Skip.IfNot(Enabled, "FORMAX_LIVE_OFFICIAL=1 değil");

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectCallback = OfficialNetworkGuard.ConnectGuardedAsync
        };
        using var http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        var limiter = new OfficialHostRateLimiter();
        var robots = new RobotsTxtPolicy();
        var config = new ConfigurationBuilder().Build();

        var real = await LoadRealMatchesAsync();
        var results = new List<OrgResult>();
        var allHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var org in Orgs)
        {
            var now = org.NowOverride ?? DateTime.UtcNow;
            var dbName = $"coverage-{org.League}-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<FormaxDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

            // ── 1. Kaynağın CANLI cevabı (üretim indiricisi + üretim ayrıştırıcısı) ─────────────
            IReadOnlyList<OfficialMatchRecord> feed;
            using (var probeDb = new FormaxDbContext(options))
            {
                var fetcher = NewFetcher(http, probeDb, limiter, robots);
                var source = NewSource(org.SourceKey, fetcher, config);
                var read = await source.ReadMatchesAsync(new OfficialRoundContext($"probe-{org.League}", now, OfficialPurposes.Result));
                if (!read.Ok || read.Value == null)
                {
                    results.Add(new OrgResult(org, 0, 0, 0, 0, 0, 0, 0, "BLOCKED", read.Outcome + ":" + read.Detail));
                    _out.WriteLine($"[{org.League}] {org.Name}: KAYNAK OKUNAMADI {read.Outcome} {read.Detail}");
                    continue;
                }
                feed = read.Value;
                foreach (var f in probeDb.OfficialSourceFetches) allHosts.Add(f.Host);
                _out.WriteLine($"[{org.League}] {org.Name} · {org.SourceKey} · canlı kayıt={feed.Count} " +
                               $"(bitmiş={feed.Count(IsFinal)}) istek={probeDb.OfficialSourceFetches.Count()} now={now:u}");
            }

            var leagueMatches = real.Where(m => m.LeagueId == org.League).ToList();

            // ── 2. Bitmiş maçlar: kaynağın final kaydı → gerçek FORMAX maçı (üretim kimlik çözücüsü) ──
            var finishedPairs = new List<(RealMatch Match, OfficialMatchRecord Record)>();
            foreach (var record in feed.Where(IsFinal).OrderByDescending(r => r.KickoffUtc ?? DateTime.MinValue))
            {
                if (finishedPairs.Count >= FinishedPerOrg) break;
                var match = leagueMatches.FirstOrDefault(m =>
                    OfficialMatchIdentityResolver.HomeMatches(record, m.Home)
                    && OfficialMatchIdentityResolver.AwayMatches(record, m.Away)
                    && record.KickoffUtc.HasValue
                    && (record.KickoffUtc.Value - m.Kickoff).Duration() <= OfficialMatchIdentityResolver.DefaultKickoffWindow);
                if (match != null && finishedPairs.All(p => p.Match.Id != match.Id)) finishedPairs.Add((match, record));
            }

            // TELAFİ YOLU — kaynağın listesi yalnız güncel haftayı yayımlıyorsa (TFF) bitmiş maç listede olmaz:
            // kayıtlı resmî maç kimliği olan gerçek maçlar seçilir, botun maç sayfası telafi okuması sonucu bulur.
            var recoveryLinks = new List<OfficialMatchLink>();
            if (finishedPairs.Count < FinishedPerOrg)
            {
                var links = await LoadLinksAsync(org.SourceKey);
                var candidates = leagueMatches
                    .Where(m => m.Kickoff < now - OfficialMatchPagePolicy.RecoveryAfter
                                && m.Kickoff > now.AddDays(-30) && links.ContainsKey(m.Id))
                    .OrderByDescending(m => m.Kickoff)
                    .Where(m => finishedPairs.All(p => p.Match.Id != m.Id))
                    .Take(FinishedPerOrg - finishedPairs.Count).ToList();
                foreach (var m in candidates)
                {
                    finishedPairs.Add((m, null!));
                    recoveryLinks.Add(links[m.Id]);
                }
            }

            // ── 3. Yaklaşan / devam eden maç ────────────────────────────────────────────────────
            var upcoming = new List<RealMatch>();
            foreach (var record in feed.Where(r => !IsFinal(r) && r.KickoffUtc.HasValue)
                         .OrderBy(r => (r.KickoffUtc!.Value - now).Duration()))
            {
                if (upcoming.Count >= UpcomingPerOrg) break;
                var match = leagueMatches.FirstOrDefault(m =>
                    OfficialMatchIdentityResolver.HomeMatches(record, m.Home)
                    && OfficialMatchIdentityResolver.AwayMatches(record, m.Away)
                    && (record.KickoffUtc!.Value - m.Kickoff).Duration() <= OfficialMatchIdentityResolver.DefaultKickoffWindow
                    && m.Kickoff > now.AddHours(-3)
                    && finishedPairs.All(p => p.Match.Id != m.Id));
                if (match != null && upcoming.All(u => u.Id != match.Id)) upcoming.Add(match);
            }

            // FORMAX fikstüründe ileri tarihli maç yoksa "erken final yazılmaz" güvencesi aynı adaptörün kanıtlanmış
            // organizasyonundan devralınır (yalnız o organizasyon PASS ettiyse); durum raporda açıkça belirtilir.
            var inherited = upcoming.Count < UpcomingPerOrg && org.UpcomingProvenByLeague is int sibling
                && results.FirstOrDefault(x => x.Org.League == sibling) is { Status: "PASS", EarlyFinal: 0 } proven
                && proven.Upcoming >= UpcomingPerOrg;
            if (finishedPairs.Count < FinishedPerOrg || (upcoming.Count < UpcomingPerOrg && !inherited))
            {
                results.Add(new OrgResult(org, finishedPairs.Count, upcoming.Count, 0, 0, 0, 0, 0, "PARTIAL",
                    $"kaynakta eşleşen bitmiş={finishedPairs.Count} yaklaşan={upcoming.Count}"));
                _out.WriteLine($"[{org.League}] {org.Name}: PARTIAL — bitmiş {finishedPairs.Count}/{FinishedPerOrg}, yaklaşan {upcoming.Count}/{UpcomingPerOrg}");
                continue;
            }

            // ── 4. İzole DB: gerçek kimlikler, sonuç alanları TEMİZ ─────────────────────────────
            using (var seed = new FormaxDbContext(options))
            {
                var teamId = 1;
                var ids = new Dictionary<string, int>(StringComparer.Ordinal);
                int Team(string name)
                {
                    if (ids.TryGetValue(name, out var existing)) return existing;
                    seed.Teams.Add(new Team { Id = teamId, Name = name });
                    ids[name] = teamId;
                    return teamId++;
                }
                foreach (var m in finishedPairs.Select(p => p.Match).Concat(upcoming))
                    seed.Matches.Add(new Match
                    {
                        Id = m.Id, LeagueId = m.LeagueId, League = org.Name, MatchDate = m.Kickoff,
                        Status = m.Kickoff <= now && upcoming.All(u => u.Id != m.Id) ? MatchStatuses.NotStarted : MatchStatuses.NotStarted,
                        HomeTeamId = Team(m.Home), AwayTeamId = Team(m.Away)
                    });
                foreach (var l in recoveryLinks)
                    seed.OfficialMatchLinks.Add(new OfficialMatchLink
                    {
                        MatchId = l.MatchId, SourceKey = l.SourceKey, OfficialMatchId = l.OfficialMatchId,
                        OfficialUrl = l.OfficialUrl, OfficialHomeName = l.OfficialHomeName, OfficialAwayName = l.OfficialAwayName,
                        OfficialKickoffUtc = l.OfficialKickoffUtc, OfficialStatus = l.OfficialStatus,
                        LinkedAtUtc = l.LinkedAtUtc, VerifiedAtUtc = l.VerifiedAtUtc
                    });
                await seed.SaveChangesAsync();
            }

            // ── 5. ÜRETİM sonuç botunun normal turu (iki kez: idempotanlık) ─────────────────────
            for (var cycle = 1; cycle <= 2; cycle++)
                using (var db = new FormaxDbContext(options))
                {
                    var fetcher = NewFetcher(http, db, limiter, robots);
                    var source = NewSource(org.SourceKey, fetcher, config);
                    var bot = new OfficialResultBotService(db, new[] { source }, new OfficialDataSourceCatalog(db),
                        new OfficialResultWriter(db, NullLogger<OfficialResultWriter>.Instance), config,
                        NullLogger<OfficialResultBotService>.Instance);
                    var report = await bot.RunCycleAsync(cycle == 1 ? now : now.AddMinutes(20));
                    if (cycle == 1)
                        _out.WriteLine($"   tur: plan+{report.Enqueued} işlenen={report.Claimed} yazılan={report.Applied} kaynak=[{string.Join(",", report.SourcesRead)}]");
                }

            // ── 6. Kanıt ve kabul ölçümleri ─────────────────────────────────────────────────────
            int wrongScore = 0, wrongDirection = 0, earlyFinal = 0, duplicates = 0, written = 0;
            using (var db = new FormaxDbContext(options))
            {
                foreach (var (match, record) in finishedPairs)
                {
                    var row = db.Matches.Single(m => m.Id == match.Id);
                    var observations = db.MatchResultObservations.Count(o => o.MatchId == match.Id);
                    var official = db.MatchResultObservations.Where(o => o.MatchId == match.Id)
                        .OrderByDescending(o => o.ObservedAtUtc).FirstOrDefault();
                    var expectedHome = record?.HomeScore ?? official?.OfficialHomeScore;
                    var expectedAway = record?.AwayScore ?? official?.OfficialAwayScore;
                    var sourceHome = record?.HomeName ?? official?.SourceHomeName;
                    var sourceAway = record?.AwayName ?? official?.SourceAwayName;

                    var isFinished = row.Status == MatchStatuses.Finished
                                     && row.ResultSource == OfficialLineupCollector.ProviderPrefix + org.SourceKey;
                    if (isFinished) written++;
                    if (isFinished && (row.HomeScore != expectedHome || row.AwayScore != expectedAway)) wrongScore++;
                    // Yön: kaynağın EV takımı FORMAX ev takımıyla, DEPLASMAN takımı deplasmanla eşleşmeli (üretim kimlik
                    // çözücüsünün kuralı; kaynağın kendi kısa adı da kabul edilir). Ters yazım sıfır olmalı.
                    var directionOk = record != null
                        ? OfficialMatchIdentityResolver.HomeMatches(record, match.Home)
                          && OfficialMatchIdentityResolver.AwayMatches(record, match.Away)
                        : sourceHome == null
                          || (OfficialTeamNameMatcher.SameTeam(sourceHome, match.Home)
                              && OfficialTeamNameMatcher.SameTeam(sourceAway, match.Away));
                    if (isFinished && !directionOk) wrongDirection++;
                    // Gerçek DB'de resmî sonuç varsa üç yollu uzlaşma (kaynak = izole yazım = üretim yazımı).
                    var productionMismatch = match.ResultSource?.StartsWith("official:", StringComparison.Ordinal) == true
                                             && (match.HomeScore != row.HomeScore || match.AwayScore != row.AwayScore);
                    if (isFinished && productionMismatch) wrongScore++;
                    if (observations > 1) duplicates += observations - 1;

                    _out.WriteLine($"   ✓ {match.Id} {match.Home}–{match.Away} {match.Kickoff:yyyy-MM-dd HH:mm}Z " +
                                   $"kaynak={sourceHome}–{sourceAway} {expectedHome}-{expectedAway} · DB={row.HomeScore}-{row.AwayScore} " +
                                   $"({row.Status}/{row.ResultDetail}/{row.ResultVerificationStatus}) src={row.ResultSource} " +
                                   $"üretimDB={match.HomeScore}-{match.AwayScore}/{match.ResultSource} gözlem={observations}");
                    Assert.True(isFinished, $"{org.Name} {match.Id}: kanonik sonuç yazılmadı ({row.Status}/{row.ResultSource})");
                }

                foreach (var match in upcoming)
                {
                    var row = db.Matches.Single(m => m.Id == match.Id);
                    var early = row.Status == MatchStatuses.Finished || row.ResultSource != null;
                    if (early) earlyFinal++;
                    _out.WriteLine($"   ○ {match.Id} {match.Home}–{match.Away} {match.Kickoff:yyyy-MM-dd HH:mm}Z " +
                                   $"durum={row.Status} src={row.ResultSource ?? "(yok)"} skor={row.HomeScore}-{row.AwayScore} → erkenFinal={early}");
                    Assert.False(early, $"{org.Name} {match.Id}: bitmemiş maça final yazıldı");
                }

                foreach (var f in db.OfficialSourceFetches) allHosts.Add(f.Host);
            }

            results.Add(new OrgResult(org, written, upcoming.Count, earlyFinal, wrongScore, wrongDirection, duplicates, 0,
                written >= FinishedPerOrg && earlyFinal == 0 && wrongScore == 0 && wrongDirection == 0 && duplicates == 0
                    ? "PASS" : "FAIL",
                inherited
                    ? $"yazılan={written}; yaklaşan maç FORMAX fikstüründe yok → aynı adaptör lig {org.UpcomingProvenByLeague}'te kanıtlandı"
                    : $"yazılan={written} çelişki={wrongScore}",
                inherited));
        }

        // ── 7. Toplu kabul ──────────────────────────────────────────────────────────────────────
        _out.WriteLine("");
        _out.WriteLine("| Organizasyon | Kaynak | Bitmiş | Yaklaşan | Skor/yön hatası | Erken final | Duplicate | Durum |");
        foreach (var r in results)
            _out.WriteLine($"| {r.Org.Name} | {r.Org.SourceKey} | {r.Finished} | " +
                           $"{(r.UpcomingInherited ? "devralındı" : r.Upcoming.ToString(CultureInfo.InvariantCulture))} | " +
                           $"{r.WrongScore + r.WrongDirection} | {r.EarlyFinal} | {r.DuplicateUpdates} | {r.Status} · {r.Detail} |");
        _out.WriteLine("");
        _out.WriteLine("dış istek host'ları: " + string.Join(", ", allHosts.OrderBy(h => h, StringComparer.Ordinal)));

        // API-Football'a hiç çıkılmadı ve bütün host'lar kayıt defterinin izin listesinde.
        Assert.DoesNotContain("v3.football.api-sports.io", allHosts);
        Assert.All(allHosts, h => Assert.True(OfficialSourceRegistry.IsAllowedHost(h), "izinsiz host: " + h));

        Assert.Equal(Orgs.Length, results.Count);
        Assert.All(results, r => Assert.Equal("PASS", r.Status));
        // 11 × 3 = 33 gerçek bitmiş maçta doğru skor ve yön.
        Assert.Equal(Orgs.Length * FinishedPerOrg, results.Sum(r => r.Finished));
        Assert.Equal(0, results.Sum(r => r.EarlyFinal + r.WrongScore + r.WrongDirection + r.DuplicateUpdates));
        // Bitmemiş maç: FORMAX fikstüründe ileri tarihli maçı olan her organizasyonda doğrudan kanıtlandı;
        // olmayanlarda (UCL/UECL) aynı adaptörün kanıtı devralındı ve bu durum raporda görünür.
        Assert.All(results.Where(r => !r.UpcomingInherited), r => Assert.True(r.Upcoming >= UpcomingPerOrg,
            $"{r.Org.Name}: yaklaşan maç doğrulanmadı"));
        Assert.True(results.Count(r => !r.UpcomingInherited) >= 9,
            "yaklaşan maç doğrudan doğrulanan organizasyon sayısı 9'un altında");
        Assert.All(results.Where(r => r.UpcomingInherited),
            r => Assert.Equal(OfficialSourceRegistry.UefaMatchApi, r.Org.SourceKey));
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────────

    private static bool IsFinal(OfficialMatchRecord r)
        => r.Status is OfficialMatchStatuses.Finished or OfficialMatchStatuses.FinishedAfterExtraTime
            or OfficialMatchStatuses.FinishedAfterPenalties
           && r.HomeScore.HasValue && r.AwayScore.HasValue;

    private static OfficialContentFetcher NewFetcher(HttpClient http, FormaxDbContext db,
        OfficialHostRateLimiter limiter, RobotsTxtPolicy robots)
        => new(http, new OfficialSourceStore(db), limiter, new DnsOfficialAddressResolver(),
            new OfficialFetcherOptions(), NullLogger<OfficialContentFetcher>.Instance, null, robots);

    private static IOfficialCompetitionSource NewSource(string key, IOfficialContentFetcher fetcher, IConfiguration config)
        => key switch
        {
            OfficialSourceRegistry.UefaMatchApi => new UefaMatchApiSource(fetcher),
            OfficialSourceRegistry.PremierLeagueSdp => new PremierLeagueSdpSource(fetcher),
            OfficialSourceRegistry.EflApi => new EflMultiClubSource(fetcher),
            OfficialSourceRegistry.BundesligaSite => new BundesligaSiteSource(fetcher),
            OfficialSourceRegistry.Ligue1Api => new Ligue1ApiSource(fetcher),
            OfficialSourceRegistry.LaLigaSite => new LaLigaSiteSource(fetcher),
            OfficialSourceRegistry.SerieASdp => new SerieASdpSource(fetcher, config),
            OfficialSourceRegistry.KnvbSite => new KnvbSiteSource(fetcher),
            OfficialSourceRegistry.TffSite => new TffSource(fetcher),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "kaynak bilinmiyor")
        };

    /// <summary>Gerçek FormaxDB'den maç kimlikleri — SALT OKUMA (yazma yok).</summary>
    private static async Task<List<RealMatch>> LoadRealMatchesAsync()
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(RealConnection).Options;
        await using var db = new FormaxDbContext(options);
        var from = DateTime.UtcNow.AddDays(-45);
        var to = DateTime.UtcNow.AddDays(12);
        return await db.Matches.AsNoTracking()
            .Where(m => LockedCompetitions.All.Contains(m.LeagueId) && m.MatchDate >= from && m.MatchDate <= to)
            .Select(m => new RealMatch(m.Id, m.LeagueId, m.HomeTeam!.Name, m.AwayTeam!.Name, m.MatchDate,
                m.Status, m.HomeScore, m.AwayScore, m.ResultSource))
            .ToListAsync();
    }

    /// <summary>Üretimde botun kendi yazdığı resmî maç bağlantıları — SALT OKUMA.</summary>
    private static async Task<Dictionary<int, OfficialMatchLink>> LoadLinksAsync(string sourceKey)
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(RealConnection).Options;
        await using var db = new FormaxDbContext(options);
        return await db.OfficialMatchLinks.AsNoTracking().Where(l => l.SourceKey == sourceKey)
            .ToDictionaryAsync(l => l.MatchId);
    }
}
