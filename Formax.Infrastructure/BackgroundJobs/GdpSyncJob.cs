using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Pipeline;
using Formax.Infrastructure.Pipeline.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// GDP pipeline zamanlayıcısı — GDP'yi manuel uçtan (GET /api/gdp/run) otomatik job'a bağlar.
    ///
    /// YENİ MİMARİ YOK: mevcut <see cref="IGlobalDataPipeline"/> sözleşmesini maç başına çağırır;
    /// 7 aşama, provider'lar ve <c>GdpMatchPersister</c> aynen kullanılır. Yeni HttpClient AÇILMAZ —
    /// api-football erişimi <c>ApiFootballGdpProvider</c> üzerinden mevcut cache + metering +
    /// resilience zincirinden geçer.
    ///
    /// KOTA TASARIMI: <c>ApiFootballSportsDataProvider.GetFixturesAsync</c> sonucu
    /// <c>fx:{from}:{to}</c> anahtarıyla 15 dakika cache'lenir. Bu yüzden maçlar
    /// <c>MatchDate</c> ARTAN sırada işlenir: aynı güne düşen maçlar arka arkaya geldiği için
    /// ilk maç cache'i doldurur, aynı günün kalan maçları API'ye hiç gitmez.
    /// <c>Gdp:MaxMatchesPerCycle</c> her turda KESİN üst sınırdır.
    ///
    /// IDEMPOTENCY: yeni duplicate sistemi yoktur. <c>GdpMatchLink.LastUpdatedUtc</c> (Persist
    /// aşamasının zaten yazdığı damga) okunur; <c>Gdp:RefreshHours</c> içinde tazelenmiş maçlar
    /// aday listesinden düşer. Aynı maç tekrar işlense bile GdpMatchPersister zaten idempotenttir.
    ///
    /// Config (bkz. appsettings "Gdp"): Enabled / RefreshMinutes / MaxMatchesPerCycle /
    /// HorizonHours / RefreshHours / RequestDelayMs.
    /// </summary>
    public sealed class GdpSyncJob : BackgroundService
    {
        /// <summary>MatchContextResolver'ın ürettiği FORMAX kimliği bu önekle kurulur.</summary>
        private const string FormaxMatchIdPrefix = "fx-match-";

        /// <summary>Cold start'ı bloklamamak için gecikmeli ilk tur (OddsIngestionJob ile aynı).</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

        private readonly ILogger<GdpSyncJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        public GdpSyncJob(
            ILogger<GdpSyncJob> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        /// <summary>Ana anahtar. Varsayılan KAPALI — açmak bilinçli bir karardır (kota tüketir).</summary>
        private bool Enabled => _config.GetValue("Gdp:Enabled", false);

        /// <summary>Turlar arası bekleme. Clamp aralığı Odds:RefreshMinutes ile aynı.</summary>
        private TimeSpan LoopDelay =>
            TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("Gdp:RefreshMinutes", 30), 5, 720));

        /// <summary>Tur başına KESİN maç tavanı. Clamp aralığı Timeline:MaxTeamsPerCycle ile aynı.</summary>
        private int MaxMatchesPerCycle =>
            Math.Clamp(_config.GetValue("Gdp:MaxMatchesPerCycle", 40), 1, 1000);

        /// <summary>Kaç saat ileriye bakılır (kickoff penceresi).</summary>
        private int HorizonHours =>
            Math.Clamp(_config.GetValue("Gdp:HorizonHours", 48), 1, 168);

        /// <summary>Aynı maç kaç saat sonra yeniden işlenir. Clamp aralığı Timeline:RefreshIntervalHours ile aynı.</summary>
        private int RefreshHours =>
            Math.Clamp(_config.GetValue("Gdp:RefreshHours", 72), 1, 100000);

        /// <summary>Maçlar arası nefes payı — dakikalık istek limiti diğer job'larla paylaşılır.</summary>
        private TimeSpan RequestDelay =>
            TimeSpan.FromMilliseconds(Math.Clamp(_config.GetValue("Gdp:RequestDelayMs", 400), 0, 5000));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Enabled)
            {
                _logger.LogInformation("[GDP JOB] disabled (Gdp:Enabled=false)");
                return;
            }

            _logger.LogInformation(
                "[GDP JOB] started (horizon={Horizon}h, max={Max}/tur, refresh={Refresh}h, döngü={Loop})",
                HorizonHours, MaxMatchesPerCycle, RefreshHours, LoopDelay);

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await RunOnceAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[GDP JOB] unhandled error in RunOnce");
                    }

                    await Task.Delay(LoopDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal kapanış
            }

            _logger.LogInformation("[GDP JOB] stopped");
        }

        /// <summary>
        /// Tek GDP turu. Admin/test ucundan da tetiklenebilir olsun diye public
        /// (OddsIngestionJob.RunOnceAsync ile aynı desen).
        /// Geri dönüş: (aday, işlenen, başarılı, başarısız).
        /// </summary>
        public async Task<(int Candidates, int Processed, int Succeeded, int Failed)> RunOnceAsync(
            CancellationToken ct)
        {
            // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir
            // (salt-ölçüm; davranış değişmez).
            using var _quotaScope = Telemetry.ApiFootballCallScope.Begin(nameof(GdpSyncJob));

            var due = await SelectDueMatchIdsAsync(ct);
            if (due.Count == 0)
            {
                _logger.LogInformation("[GDP JOB] işlenecek maç yok (horizon={Horizon}h, refresh={Refresh}h).",
                    HorizonHours, RefreshHours);
                return (0, 0, 0, 0);
            }

            int processed = 0, succeeded = 0, failed = 0;
            var delay = RequestDelay;

            foreach (var matchId in due)
            {
                if (ct.IsCancellationRequested) break;

                // Her maç KENDİ scope'unda: pipeline ve 7 aşama Scoped kayıtlıdır.
                using (var scope = _scopeFactory.CreateScope())
                {
                    var pipeline = scope.ServiceProvider.GetRequiredService<IGlobalDataPipeline>();
                    try
                    {
                        var result = await pipeline.RunAsync(new PipelineContext { MatchId = matchId }, ct);
                        processed++;
                        if (result.Success) succeeded++;
                        else
                        {
                            failed++;
                            var stage = result.Stages?.FirstOrDefault(s => !s.Success);
                            _logger.LogWarning(
                                "[GDP JOB] match {MatchId} pipeline başarısız — aşama={Stage} hata={Error}",
                                matchId, stage?.Stage, stage?.Error);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Bir maçın hatası turu DURDURMAZ — logla, sonraki maça geç.
                        processed++;
                        failed++;
                        _logger.LogError(ex, "[GDP JOB] match {MatchId} işlenemedi", matchId);
                    }
                }

                if (delay > TimeSpan.Zero && !ct.IsCancellationRequested)
                    await Task.Delay(delay, ct);
            }

            _logger.LogInformation(
                "[GDP JOB] tur bitti: aday={Candidates} işlenen={Processed} başarılı={Ok} başarısız={Failed}",
                due.Count, processed, succeeded, failed);

            return (due.Count, processed, succeeded, failed);
        }

        /// <summary>
        /// Bu turda GDP'ye gönderilecek maç id'leri. SALT-OKUMA; hiçbir API çağrısı yapmaz.
        /// Testten doğrudan çağrılabilsin diye public.
        ///
        /// Filtreler:
        ///   1. Status = "NotStarted" ve kickoff (now, now+HorizonHours] aralığında  → SQL
        ///   2. CoveragePolicy allow-list (boş liste = kısıtlama yok)                → bellek
        ///      (WorldPerceptionDailyJob:524 ile aynı desen; Allows SQL'e çevrilemez)
        ///   3. GdpMatchLink.LastUpdatedUtc son RefreshHours içindeyse ELENİR        → SQL + bellek
        ///   4. MatchDate ARTAN sıra (aynı gün ardışık → fixture cache paylaşımı)
        ///   5. MaxMatchesPerCycle kesin tavan
        /// </summary>
        public async Task<IReadOnlyList<int>> SelectDueMatchIdsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<FormaxDbContext>();
            var config = sp.GetRequiredService<IConfiguration>();

            var utcNow = DateTime.UtcNow;
            var horizonEnd = utcNow.AddHours(HorizonHours);
            var refreshCutoff = new DateTimeOffset(utcNow.AddHours(-RefreshHours), TimeSpan.Zero);

            var rows = await db.Matches.AsNoTracking()
                .Where(m => m.Status == "NotStarted"
                         && m.MatchDate > utcNow
                         && m.MatchDate <= horizonEnd)
                .OrderBy(m => m.MatchDate)
                .Select(m => new { m.Id, m.MatchDate, m.LeagueId })
                .ToListAsync(ct);

            // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
            var allow = CoveragePolicy.LeagueAllowList(config);
            var scoped = rows.Where(r => CoveragePolicy.Allows(allow, r.LeagueId)).ToList();
            if (scoped.Count == 0) return Array.Empty<int>();

            // Watermark: yalnız SON RefreshHours içinde güncellenmiş link'ler çekilir (küçük küme;
            // maç id listesini IN(...) ile göndermeye gerek yok). GdpMatchLink.MatchId canonical
            // Match.Id'dir → doğrudan karşılaştırılır.
            var freshMatchIds = (await db.GdpMatchLinks.AsNoTracking()
                    .Where(l => l.LastUpdatedUtc >= refreshCutoff)
                    .Select(l => l.MatchId)
                    .ToListAsync(ct))
                .ToHashSet();

            var due = scoped
                .Where(r => !freshMatchIds.Contains(r.Id))
                .OrderBy(r => r.MatchDate)
                .Take(MaxMatchesPerCycle)
                .Select(r => r.Id)
                .ToList();

            _logger.LogInformation(
                "[GDP JOB] aday seçimi: pencere={Window} allow-list sonrası={Scoped} taze={Fresh} → seçilen={Due} (tavan {Max})",
                rows.Count, scoped.Count, freshMatchIds.Count, due.Count, MaxMatchesPerCycle);

            return due;
        }

        /// <summary>Bir Match için GDP kimliği (MatchContextResolver ile aynı kural).</summary>
        public static string FormaxMatchIdFor(int matchId) => FormaxMatchIdPrefix + matchId;
    }
}
