using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Discovery;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// FORMAX Data Engine v2 — Global News Discovery scheduler.
    ///
    /// Fixture Discovery'den sonra çalışır. Job'ın kendi kalp atışı 5 dakikadır; bir maçın
    /// GERÇEKTEN ne sıklıkta taranacağı KICKOFF YAKINLIĞINA göre belirlenir (kademeli refresh):
    ///
    ///   • kickoff'a 0–30 dk (ve başlamış maçlar, −3s..0) → 5 dk
    ///   • kickoff'a 30–60 dk                            → 10 dk
    ///   • kickoff'a 1–3 saat                            → 15 dk
    ///   • kickoff'a 3+ saat (…+7 gün)                   → 30 dk
    ///   • BİTMİŞ maçlar                                 → işlenmez
    ///
    /// ÖNCEKİ DAVRANIŞ: yaklaşan maçların tamamı 3 döngüde bir (15 dk) taranıyordu; kickoff'a
    /// göre sıklaşma YOKTU (production'da ölçüldü: 18:31:28 → 18:48:34 = 17,1 dk, maça kalan
    /// süreden bağımsız). Artık maç yaklaştıkça daha sık taranır, uzaktaki maçlar seyrelir →
    /// aynı HTTP bütçesi kickoff'a yakın maçlara kaydırılır. Yeni job/tablo/provider YOK.
    ///
    /// Her maç için query üretir → açık provider'ları arar → dedup/cluster/confidence →
    /// MatchNewsArticles'a (FORMAX_MATCH_ID altında) upsert eder.
    /// Mevcut Reasoning/LLM'e dokunmaz.
    /// </summary>
    public sealed class NewsDiscoveryJob : BackgroundService
    {
        private static readonly TimeSpan Cycle = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(40);
        private const int UpcomingHorizonDays = 7;
        private const int MaxPerCycle = 25;           // HTTP yükünü sınırla

        // ── Kademeli refresh: kickoff'a kalan dakika → tarama aralığı (dakika) ──
        private const int TierImminentMinutes = 30;   // 0–30 dk
        private const int TierNearMinutes     = 60;   // 30–60 dk
        private const int TierSoonMinutes     = 180;  // 1–3 saat
        private const int TierFlashMinutes    = 720;  // 3–12 saat (maç günü penceresi)
        private const int RefreshImminent = 5;
        private const int RefreshNear     = 10;
        private const int RefreshSoon     = 15;
        private const int RefreshFlash    = 30;
        private const int RefreshDistant  = 120;      // 12 saat+ → seyrek (kota bütçesi acil maça kayar)

        // ── Sorgu bütçesi (provider başına): maç yaklaştıkça daha derin tarama ──
        private const int BudgetImminent = 8;
        private const int BudgetNear     = 6;
        private const int BudgetSoon     = 5;
        private const int BudgetFlash    = 4;
        private const int BudgetDistant  = 3;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NewsDiscoveryJob> _logger;

        /// <summary>FORMAX_MATCH_ID → o maçın en son tarandığı an (süreç-içi; DB şeması değişmez).
        /// Yeniden başlatmada boştur → her maç bir kez taranır (mevcut cold-start davranışı).</summary>
        private readonly Dictionary<string, DateTime> _lastScan = new(StringComparer.Ordinal);

        public NewsDiscoveryJob(IServiceScopeFactory scopeFactory, ILogger<NewsDiscoveryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[NEWS] Discovery scheduler started.");
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[NEWS] Discovery döngüsü başarısız."); }

                try { await Task.Delay(Cycle, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<Data.FormaxDbContext>();
            var config = sp.GetRequiredService<IConfiguration>();
            var matchIdFactory = sp.GetRequiredService<Formax.Application.Services.Fixtures.FormaxMatchIdFactory>();
            var discovery = sp.GetRequiredService<GlobalNewsDiscoveryService>();
            var newsRepo = sp.GetRequiredService<IMatchNewsRepository>();
            var intelligence = sp.GetRequiredService<MatchIntelligenceService>();
            var evidenceRepo = sp.GetRequiredService<IMatchEvidenceRepository>();

            var now = DateTime.UtcNow;

            // ── ADAY EVRENİ: Matches (ürünün ve AI'ın gerçek maç evreni) ────────────────
            // ÖNCEKİ DAVRANIŞ: adaylar Fixtures tablosundan geliyordu; o tablo ayrı bir
            // ingestion hattından (FixtureDiscoveryJob) beslendiği için ürünün Matches
            // evreniyle örtüşmüyordu → üretilen FORMAX_MATCH_ID'ler AI'ın sorguladığı
            // kimliklerle eşleşmiyor, haber maça hiç ulaşmıyordu.
            // ARTIK: kimlik AI ile AYNI kaynaktan (Matches + FormaxMatchIdFactory) üretilir.
            var windowEnd = now.AddDays(UpcomingHorizonDays);
            var allow = CoveragePolicy.LeagueAllowList(config);

            var rows = await db.Matches.AsNoTracking()
                .Where(m => m.Status != "Finished"
                         && m.MatchDate >= now.AddHours(-3)
                         && m.MatchDate <= windowEnd)
                .OrderBy(m => m.MatchDate)
                .Select(m => new
                {
                    m.Id,
                    m.MatchDate,
                    m.League,
                    m.LeagueId,
                    Home = m.HomeTeam!.Name,
                    Away = m.AwayTeam!.Name
                })
                .ToListAsync(ct);

            // MVP kapsam daraltması (Coverage:LeagueAllowList) — boş liste = kısıtlama yok.
            var scoped = rows.Where(r => CoveragePolicy.Allows(allow, r.LeagueId)).ToList();

            var universe = scoped
                .Where(r => !string.IsNullOrWhiteSpace(r.Home) && !string.IsNullOrWhiteSpace(r.Away))
                .Select(r => new
                {
                    FormaxMatchId = SafeId(matchIdFactory, r.MatchDate, r.Home!, r.Away!),
                    HomeTeam = r.Home!,
                    AwayTeam = r.Away!,
                    League = r.League ?? string.Empty,
                    KickoffUtc = r.MatchDate
                })
                .Where(x => x.FormaxMatchId != null)
                .GroupBy(x => x.FormaxMatchId!).Select(g => g.First())
                .ToList();

            // Pencereden çıkan maçların son-tarama kaydını bırak (sözlük sınırsız büyümesin).
            var live = universe.Select(x => x.FormaxMatchId!).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in _lastScan.Keys.Where(k => !live.Contains(k)).ToList())
                _lastScan.Remove(stale);

            // ── KADEMELİ REFRESH: yalnız kendi aralığı DOLMUŞ maçlar taranır ────────────
            // Hiç taranmamış maç her zaman "vadesi gelmiş" sayılır (ilk kapsama).
            //
            // DARBOĞAZ DÜZELTİLDİ: sıralama yalnız kickoff'a göreydi; cold-start'ta 7 günlük
            // pencerede vadesi dolan onlarca UZAK maç kuyruğu doldurduğu için 25 maçlık tur
            // bütçesi tükeniyor, kickoff'a 20 dakika kalan maç sıraya giremiyordu. Artık
            // sıralama önce ACİLİYET KADEMESİ (refresh aralığı), sonra kickoff.
            var targets = universe
                .Where(x => IsDue(x.FormaxMatchId!, x.KickoffUtc, now))
                .OrderBy(x => RefreshMinutes(x.KickoffUtc, now))   // 5dk kademesi her zaman önce
                .ThenBy(x => x.KickoffUtc)
                .Take(MaxPerCycle)
                .ToList();

            // ── SIRADAKİ MAÇ HARİTASI: tek takım haberi ("Fenerbahçe'de sakatlık") yalnız o
            // takımın EN YAKIN maçına bağlanabilir; aksi hâlde aynı haber takımın 7 gündeki
            // tüm maçlarına kopyalanır ve her maç "son gelişme" sanır.
            var nextMatchByTeam = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in universe.OrderBy(x => x.KickoffUtc))
            {
                if (!nextMatchByTeam.ContainsKey(m.HomeTeam)) nextMatchByTeam[m.HomeTeam] = m.FormaxMatchId!;
                if (!nextMatchByTeam.ContainsKey(m.AwayTeam)) nextMatchByTeam[m.AwayTeam] = m.FormaxMatchId!;
            }

            if (targets.Count == 0)
            {
                _logger.LogInformation(
                    "[NEWS] Cycle — {Universe} MVP maçın hiçbirinin refresh aralığı dolmadı; tarama yok.",
                    universe.Count);
                return;
            }

            // Güvenlik kapıları (config; varsayılanlar appsettings "News" bölümünde).
            var minQuality = Math.Clamp(config.GetValue("News:MinEvidenceSourceQuality", 85), 0, 100);

            // RequireBothTeams=true KATI moddur: haber metni HEM ev HEM deplasman takımını
            // anmalıdır. Ölçüldü — gerçek futbol haberlerinin ezici çoğunluğu tek takım
            // hakkındadır ("Fenerbahçe'de sakatlık"), bu yüzden katı mod gerçek gelişmeleri
            // topluca eliyordu. Varsayılan artık takım-farkındalıklı mod: tek takım haberi
            // YALNIZ o takımın sıradaki maçına, yalnız maç öncesi penceresinde ve yabancı
            // fikstürden söz etmiyorsa bağlanır (bkz. MatchIntelligenceService.ResolveRelation).
            var requireBothTeams = config.GetValue("News:RequireBothTeams", false);

            var totalAdded = 0;
            var totalEvidence = 0;
            int gFresh = 0, gQuality = 0, gRelevance = 0, gContent = 0, gSeen = 0,
                gSport = 0, gHistory = 0;

            foreach (var f in targets)
            {
                ct.ThrowIfCancellationRequested();

                // Tarama denemesi kaydedilir (haber çıkmasa bile) → aynı maç aralık dolmadan
                // tekrar taranmaz; tek maçın kuyruğu kilitlemesi önlenir.
                _lastScan[f.FormaxMatchId!] = DateTime.UtcNow;

                var refresh = RefreshMinutes(f.KickoffUtc, now);
                var (items, _) = await discovery.DiscoverForMatchAsync(
                    f.FormaxMatchId!, f.HomeTeam, f.AwayTeam, f.League, string.Empty, f.KickoffUtc, ct,
                    queryBudget: QueryBudget(refresh),
                    urgent: refresh <= RefreshNear);

                // Ham haber KEŞİF katmanında saklanmaya devam eder (Tier-3 dahil).
                totalAdded += await newsRepo.UpsertAsync(f.FormaxMatchId!, items, ct);

                // v2.1 — AI'ın FACTUAL katmanı: yalnız futbol + içerik türü + tazelik + kalite +
                // maç bağlama kapılarından geçen haber Evidence olur.
                // "Haber bulundu" ≠ "FORMAX bunu gerçek kabul eder".
                var evidence = intelligence.BuildEvidence(
                    f.FormaxMatchId!, items, f.HomeTeam, f.AwayTeam, minQuality, requireBothTeams,
                    kickoffUtc: f.KickoffUtc,
                    homeIsNextMatch: IsNextMatch(nextMatchByTeam, f.HomeTeam, f.FormaxMatchId!),
                    awayIsNextMatch: IsNextMatch(nextMatchByTeam, f.AwayTeam, f.FormaxMatchId!));

                var st = intelligence.LastGateStats;
                gSeen += st.Total; gFresh += st.DroppedFreshness;
                gQuality += st.DroppedQuality; gRelevance += st.DroppedRelevance;
                gContent += st.DroppedContent; gSport += st.DroppedNonFootball;
                gHistory += st.DroppedHistorical;

                totalEvidence += await evidenceRepo.UpsertAsync(f.FormaxMatchId!, evidence, ct);
            }

            _logger.LogInformation(
                "[NEWS] Cycle — {Matches}/{Universe} maç tarandı (öncelik: acil kademe önce), " +
                "{Added} yeni haber, {Evidence} yeni kanıt. " +
                "Kademe: 0-30dk={T1}, 30-60dk={T2}, 1-3sa={T3}, 3-12sa={T4}, 12sa+={T5}. " +
                "Kapılar: {Seen} aday → futbol dışı -{Sport}, içerik türü -{Content}, " +
                "eski/tarihsel -{History}, freshness -{Fresh}, quality<{MinQ} -{Qual}, maç bağlama -{Rel}.",
                targets.Count, universe.Count, totalAdded, totalEvidence,
                targets.Count(t => RefreshMinutes(t.KickoffUtc, now) == RefreshImminent),
                targets.Count(t => RefreshMinutes(t.KickoffUtc, now) == RefreshNear),
                targets.Count(t => RefreshMinutes(t.KickoffUtc, now) == RefreshSoon),
                targets.Count(t => RefreshMinutes(t.KickoffUtc, now) == RefreshFlash),
                targets.Count(t => RefreshMinutes(t.KickoffUtc, now) == RefreshDistant),
                gSeen, gSport, gContent, gHistory, gFresh, minQuality, gQuality, gRelevance);
        }

        /// <summary>
        /// Bu maçın refresh aralığı (dakika) — YALNIZ kickoff yakınlığından türer.
        /// Başlamış maç (kickoff geçmiş, −3s penceresi içinde) en acil kademededir.
        /// </summary>
        internal static int RefreshMinutes(DateTime kickoffUtc, DateTime nowUtc)
        {
            var minutesToKickoff = (kickoffUtc - nowUtc).TotalMinutes;
            if (minutesToKickoff <= TierImminentMinutes) return RefreshImminent;   // başlamış + 0–30 dk
            if (minutesToKickoff <= TierNearMinutes)     return RefreshNear;       // 30–60 dk
            if (minutesToKickoff <= TierSoonMinutes)     return RefreshSoon;       // 1–3 saat
            if (minutesToKickoff <= TierFlashMinutes)    return RefreshFlash;      // 3–12 saat
            return RefreshDistant;                                                 // 12 saat+
        }

        /// <summary>Provider başına sorgu bütçesi — maç yaklaştıkça derinleşir, uzakta daralır.</summary>
        private static int QueryBudget(int refreshMinutes) => refreshMinutes switch
        {
            RefreshImminent => BudgetImminent,
            RefreshNear     => BudgetNear,
            RefreshSoon     => BudgetSoon,
            RefreshFlash    => BudgetFlash,
            _               => BudgetDistant
        };

        /// <summary>Bu maç, takımın SIRADAKİ (en yakın) maçı mı?</summary>
        private static bool IsNextMatch(
            Dictionary<string, string> nextMatchByTeam, string team, string formaxMatchId) =>
            !nextMatchByTeam.TryGetValue(team, out var id) || string.Equals(id, formaxMatchId, StringComparison.Ordinal);

        /// <summary>Maçın kendi kademesine göre tarama vakti geldi mi? Hiç taranmamışsa evet.</summary>
        private bool IsDue(string formaxMatchId, DateTime kickoffUtc, DateTime nowUtc)
        {
            if (!_lastScan.TryGetValue(formaxMatchId, out var last)) return true;
            return (nowUtc - last).TotalMinutes >= RefreshMinutes(kickoffUtc, nowUtc);
        }

        /// <summary>FORMAX_MATCH_ID üretimi; kimlik çözülemezse maç atlanır (uydurma kimlik yok).</summary>
        private static string? SafeId(
            Formax.Application.Services.Fixtures.FormaxMatchIdFactory factory,
            DateTime kickoffUtc, string home, string away)
        {
            try { return factory.Create(kickoffUtc, home, away); }
            catch { return null; }
        }
    }
}
