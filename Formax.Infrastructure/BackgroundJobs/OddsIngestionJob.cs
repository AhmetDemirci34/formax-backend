using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// GERÇEK market oranı ingestion'ı — api-football GET /odds?date=&amp;page=.
    ///
    /// Neden gün+sayfa: maç-başına /odds?fixture= çağrısı bugünün ~700 maçı için ~700 istek eder;
    /// gün bazlı sayfalı form aynı kapsamı ~40 istekte verir (Pro plan 7.500/gün).
    ///
    /// Akış: gün penceresindeki sayfaları çek → sağlayıcı fixture id'sini Match.ExternalMatchId
    /// ile eşle → normalize market anahtarlarını MatchMarketOdds'a upsert et.
    /// Eşleşmeyen fixture ATLANIR; hiçbir oran türetilmez/uydurulmaz.
    ///
    /// İKİ KADEMELİ PENCERE (keşif ufku ≠ oran ufku):
    ///  • TAZELİK — <c>Odds:DayWindow</c> günü HER turda taranır. Yaklaşan maçlarda oran oynar;
    ///    sık okuma hareket yönünü (PreviousOdd) besler.
    ///  • KEŞİF — <c>Odds:ExtendedDayWindow</c> günü <c>Odds:ExtendedRefreshHours</c> aralığıyla
    ///    taranır. FORMAX 20 gün sonrasını gösterebilir ama sağlayıcı oranı ~7-8 gün önce
    ///    yayımlar; oran yayımlandığı ANDA bir sonraki keşif turunda DB'ye girer. Oranı olmayan
    ///    maç null kalır (uydurulmaz) ve ufuk kaydıkça yeniden denenir.
    /// </summary>
    public sealed class OddsIngestionJob : BackgroundService
    {
        private readonly ILogger<OddsIngestionJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        public OddsIngestionJob(
            ILogger<OddsIngestionJob> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        private bool Enabled => _config.GetValue("Odds:Enabled", true);

        /// <summary>Kaç günlük ileri pencere HER turda taranır (bugün dahil) — tazelik penceresi.</summary>
        private int DayWindow => Math.Clamp(_config.GetValue("Odds:DayWindow", 2), 1, 7);

        /// <summary>
        /// KEŞİF penceresi: sağlayıcı oranı yayımladığı anda yakalanabilsin diye periyodik olarak
        /// taranan daha uzun ufuk. Her turda değil, <see cref="ExtendedRefreshHours"/> aralığıyla.
        ///
        /// Neden gerekli (ölçüm, 17.08): fikstür 1584376 (Alanyaspor–Beşiktaş, 23 Ağu) için
        /// /odds?fixture= gerçek oran döndürüyor (Çifte Şans Home/Draw = 1.91) ama o gün
        /// DayWindow=2 penceresinin dışında kaldığı için hiç sorgulanmıyordu → MatchMarketOdds 0
        /// → Decision DTO odd=null → UI oran gösteremiyor.
        ///
        /// Neden 8 gün varsayılan (ölçüm, aynı gün): /odds?date= yanıtları — 18 Ağu 7 sayfa,
        /// 23 Ağu 15 sayfa (DOLU), 29 Ağu 0 sonuç, 5 Eyl 0 sonuç. Sağlayıcının pre-match oran
        /// ufku ~7-8 gün; daha uzağı taramak boş sayfa maliyetinden ibaret. Ufuk her gün
        /// kaydığı için tarama periyodik tekrarlanır (kontrollü yeniden deneme).
        /// </summary>
        private int ExtendedDayWindow =>
            Math.Clamp(_config.GetValue("Odds:ExtendedDayWindow", 8), 1, 21);

        /// <summary>Keşif taramasının tekrar aralığı (saat). Kota koruması.</summary>
        private int ExtendedRefreshHours =>
            Math.Clamp(_config.GetValue("Odds:ExtendedRefreshHours", 6), 1, 48);

        /// <summary>
        /// Son keşif taramasının zamanı. Job Singleton kaydedildiği için (Program.cs:
        /// AddSingleton + AddHostedService(sp => sp.GetRequiredService&lt;OddsIngestionJob&gt;()))
        /// turlar arasında yaşar. Kalıcı depolama gerektirmez → migration YOK.
        /// </summary>
        private DateTime _lastExtendedPassUtc = DateTime.MinValue;

        /// <summary>Gün başına en fazla kaç sayfa (kota koruması). 0 = sınırsız.</summary>
        private int MaxPagesPerDay => Math.Max(0, _config.GetValue("Odds:MaxPagesPerDay", 60));

        /// <summary>Sayfalar arası nefes payı — dakikalık istek limiti diğer job'larla paylaşılır.</summary>
        private TimeSpan RequestDelay =>
            TimeSpan.FromMilliseconds(Math.Clamp(_config.GetValue("Odds:RequestDelayMs", 400), 0, 5000));

        /// <summary>Limit aşımı (boş sayfa) sonrası bekleme.</summary>
        private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(20);

        private TimeSpan LoopDelay =>
            TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("Odds:RefreshMinutes", 30), 5, 720));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Enabled)
            {
                _logger.LogInformation("[ODDS JOB] disabled (Odds:Enabled=false)");
                return;
            }

            _logger.LogInformation("[ODDS JOB] started");

            // Cold start'ı bloklamamak için gecikmeli ilk tur (Backend Freeze kuralı).
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ODDS JOB] unhandled error in RunOnce");
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        /// <summary>
        /// Tek ingestion turu. Admin ucundan da tetiklenebilir olsun diye public.
        /// Geri dönüş: (işlenen sayfa, eşleşen maç, yazılan market satırı).
        ///
        /// Turlar SIRAYA ALINIR: döngü ile admin manuel tetiği çakışırsa iki tur da aynı maç
        /// için "satır yok" okur ve aynı (MatchId, MarketKey) için INSERT dener; unique index
        /// IX_MatchMarketOdds_MatchId_MarketKey bunu reddeder (veri bozulmaz) ama DbUpdateException
        /// turu yarıda keser ve kalan günler işlenmez. Ölçüldü (17.08): duplicate key (17915, MS1).
        /// Keşif turu ~85 sn sürdüğü için çakışma ihtimali gerçektir.
        /// </summary>
        public async Task<(int Pages, int MatchesMatched, int MarketRows)> RunOnceAsync(CancellationToken ct)
        {
            await _runGate.WaitAsync(ct);
            try
            {
                return await RunCycleAsync(ct);
            }
            finally
            {
                _runGate.Release();
            }
        }

        /// <summary>Turu sıraya alan kapı — aynı anda tek ingestion turu çalışır.</summary>
        private readonly SemaphoreSlim _runGate = new(1, 1);

        private async Task<(int Pages, int MatchesMatched, int MarketRows)> RunCycleAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(OddsIngestionJob));

            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var provider = sp.GetRequiredService<ISportsDataProvider>();
            var oddsRepo = sp.GetRequiredService<IMatchOddsRepository>();
            var db = sp.GetRequiredService<FormaxDbContext>();

            var today = DateTime.UtcNow.Date;

            // Bu turda kaç gün taranacak: her tur tazelik penceresi, periyodik olarak keşif
            // penceresi. Keşif turu geldiyse pencere genişler — idMap de AYNI genişlikte
            // kurulmalı, yoksa çekilen oran aşağıdaki fixture eşleşmesinde düşerdi.
            var extendedDue =
                (DateTime.UtcNow - _lastExtendedPassUtc).TotalHours >= ExtendedRefreshHours;
            var scanDays = extendedDue ? Math.Max(DayWindow, ExtendedDayWindow) : DayWindow;
            var windowEnd = today.AddDays(scanDays);

            // Sağlayıcı fixture id → Formax MatchId. Yalnız pencere içindeki maçlar (tek sorgu).
            var idMap = await db.Matches
                .AsNoTracking()
                .Where(m => m.MatchDate >= today.AddDays(-1)
                         && m.MatchDate < windowEnd.AddDays(1)
                         && m.ExternalMatchId != null)
                .Select(m => new { m.Id, m.ExternalMatchId })
                .ToDictionaryAsync(x => x.ExternalMatchId!, x => x.Id, ct);

            if (idMap.Count == 0)
            {
                _logger.LogInformation("[ODDS JOB] pencerede ExternalMatchId'li maç yok — atlandı");
                return (0, 0, 0);
            }

            var pages = 0;
            var matched = 0;
            var rows = 0;

            // KAPSAM + OYNANMAMIŞLIK: /odds?date= o günün TÜM liglerini döndürür, o yüzden gün
            // seviyesinde karar veriyoruz. FORMAX kapsamında ve HENÜZ OYNANMAMIŞ maçı olmayan gün
            // için sağlayıcıya HİÇ çıkılmaz (bitmiş maçın oranı hiçbir yerde kullanılmıyor).
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var scopedDays = await db.Matches.AsNoTracking()
                .Where(m => m.MatchDate >= today && m.MatchDate < windowEnd.AddDays(1)
                         && m.Status != "Finished" && m.Status != "Cancelled" && m.ExternalMatchId != null)
                .Select(m => new { m.MatchDate, m.LeagueId })
                .ToListAsync(ct);
            var daysWithScopedMatches = scopedDays
                .Where(x => CoveragePolicy.Allows(allow, x.LeagueId))
                .Select(x => x.MatchDate.Date)
                .ToHashSet();

            for (var day = 0; day < scanDays && !ct.IsCancellationRequested; day++)
            {
                var date = today.AddDays(day);

                if (!daysWithScopedMatches.Contains(date.Date))
                {
                    _logger.LogDebug("[ODDS JOB] {Date} — kapsamda oynanmamış maç yok, sağlayıcıya çıkılmadı.", date);
                    continue;
                }

                var page = 1;
                var totalPages = 1;
                var emptyStreak = 0;

                do
                {
                    var result = await provider.GetOddsByDateAsync(date, page, ct);
                    pages++;

                    // Sağlayıcının bildirdiği toplam sayfa sayısı KORUNUR. Boş yanıtta 1'e
                    // düşürmek, geçici bir dakikalık limit aşımının tüm günü kesmesine yol
                    // açıyordu (43 sayfalık günde 4. sayfada duruyordu).
                    if (result.TotalPages > 0) totalPages = result.TotalPages;

                    if (result.Items.Count == 0)
                    {
                        // Sağlayıcı SAYFALAMA bilgisi döndürdüyse (TotalPages >= 1) ilk sayfanın
                        // boş olması gerçek cevaptır: o gün için henüz oran yayımlanmamış.
                        // Ölçüldü (17.08): date=2026-08-29 → results 0, paging.total 1.
                        // Bunu 3 kez denemek 3 istek + 40 sn boşa gider; keşif penceresi uzak
                        // günleri kapsadığı için bu maliyet her turda katlanırdı. Gün 1 istekte
                        // kapanır, ufuk kaydıkça sonraki turlarda yeniden denenir.
                        if (result.TotalPages > 0 && page == 1) break;

                        // TotalPages == 0 → sayfalama hiç gelmedi (dakikalık limit aşımı/hata).
                        // Eski davranış korunur: bekle ve aynı sayfayı yeniden dene.
                        emptyStreak++;
                        if (emptyStreak >= 3) break;
                        await Task.Delay(RateLimitBackoff, ct);
                        continue;
                    }

                    emptyStreak = 0;

                    foreach (var fx in result.Items)
                    {
                        if (!idMap.TryGetValue(fx.FixtureExternalId, out var matchId)) continue;
                        if (fx.Markets.Count == 0) continue;

                        var payload = fx.Markets.ToDictionary(
                            kv => kv.Key,
                            kv => (kv.Value.Odd, kv.Value.BookmakerId, kv.Value.BookmakerName));

                        rows += await oddsRepo.UpsertAsync(matchId, payload, ct);
                        matched++;
                    }

                    page++;
                    if (MaxPagesPerDay > 0 && page > MaxPagesPerDay) break;

                    // Dakikalık limiti diğer job'larla paylaşıyoruz; sayfalar arası nefes payı.
                    if (RequestDelay > TimeSpan.Zero) await Task.Delay(RequestDelay, ct);
                }
                while (page <= totalPages && !ct.IsCancellationRequested);
            }

            // Keşif taraması yalnız TAMAMLANDIĞINDA damgalanır; iptal edilen tur bir sonraki
            // turda yeniden denenir (yarım kalan pencere "yapıldı" sayılmaz).
            if (extendedDue && !ct.IsCancellationRequested)
                _lastExtendedPassUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "[ODDS JOB] {Days} gün ({Mode}) · {Pages} sayfa · {Matched} maç eşleşti · {Rows} market satırı",
                scanDays, extendedDue ? "keşif" : "tazelik", pages, matched, rows);

            return (pages, matched, rows);
        }
    }
}
