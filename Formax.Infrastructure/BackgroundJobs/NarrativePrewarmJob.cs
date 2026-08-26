using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.Radar;
using Formax.Application.Interfaces;
using Formax.Application.UseCases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// AI ANLATI ISITMA (PREWARM) — kullanıcı maç detayını açtığında bulut LLM BEKLEMESİN.
    ///
    /// SORUN (ölçüldü): <c>/detail</c> soğukken üç anlatı yüzeyi (Discover + MatchDetail +
    /// AiIncele) istek yolunda üretiliyordu; profil <c>narrative(LLM)=20.559ms</c>,
    /// toplamın %99'u. Sıcakken aynı uç 0,12-0,17 sn.
    ///
    /// ÇÖZÜM: yaklaşan maçların anlatısını ARKA PLANDA önceden üret → kullanıcının isteği
    /// hazır snapshot'a düşer.
    ///
    /// AI MAÇ ANALİZİ DEĞİŞMEZ: aynı use case, aynı context, aynı prompt, aynı guard, aynı
    /// cache. Yalnız ÜRETİM ZAMANI değişir. Uç, model ve çıktı aynıdır; AI alanı boş kalmaz.
    ///
    /// LLM MALİYETİ KONTROLLÜ:
    ///  • Yalnız kilitli kapsam içindeki (repository zaten süzüyor) yaklaşan maçlar.
    ///  • Tur başına en fazla <see cref="MaxMatchesPerCycle"/> maç.
    ///  • Bir maç yeniden ısıtılmadan önce <see cref="RewarmAfter"/> geçmeli; maça
    ///    <see cref="FinalApproachHours"/> saatten az kaldıysa daha sık tazelenir.
    ///  • Context değişmediyse pipeline'ın kendi context-hash cache'i zaten LLM ÇAĞIRMAZ;
    ///    bu job yalnızca üretimi kullanıcıdan önceye alır.
    ///
    /// HATA: maç bazında yakalanır, loglanır, döngü devam eder. Sonsuz retry YOKTUR —
    /// bir sonraki turda normal sırayla tekrar denenir.
    /// </summary>
    public sealed class NarrativePrewarmJob : BackgroundService
    {
        /// <summary>Isıtma turu aralığı.</summary>
        private static readonly TimeSpan Cycle = TimeSpan.FromMinutes(20);

        /// <summary>Uygulama açılışında diğer job'lar yerleşsin diye beklenen süre.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);

        /// <summary>Kaç saat sonrasına kadar olan maçlar ısıtılır.</summary>
        private const int HorizonHours = 24;

        /// <summary>Bir maçın normal yeniden ısıtma aralığı.</summary>
        private static readonly TimeSpan RewarmAfter = TimeSpan.FromHours(6);

        /// <summary>Maça bu kadar saat kaldığında daha sık tazelenir (kadro/sakatlık değişir).</summary>
        private const int FinalApproachHours = 3;

        /// <summary>Son yaklaşma penceresindeki yeniden ısıtma aralığı.</summary>
        private static readonly TimeSpan FinalApproachRewarmAfter = TimeSpan.FromMinutes(60);

        /// <summary>Tur başına en fazla ısıtılacak maç (LLM maliyet tavanı).</summary>
        private const int MaxMatchesPerCycle = 12;

        /// <summary>Maç başına ısıtma süresi tavanı — takılı bir çağrı turu kilitlemesin.</summary>
        private static readonly TimeSpan PerMatchTimeout = TimeSpan.FromSeconds(90);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<NarrativePrewarmJob> _logger;

        /// <summary>Maç → en son başarılı ısıtma zamanı. Süreç ömrü boyunca bellek içi.</summary>
        private readonly ConcurrentDictionary<int, DateTime> _lastWarmed = new();

        public NarrativePrewarmJob(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<NarrativePrewarmJob> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Kapatma anahtarı: <c>Narrative:PrewarmEnabled</c>. Varsayılan AÇIK; kapatılırsa
        /// davranış eski hâline döner (anlatı kullanıcı isteğinde üretilir).
        /// </summary>
        private bool IsEnabled => _config.GetValue("Narrative:PrewarmEnabled", true);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!IsEnabled)
            {
                _logger.LogInformation("[PREWARM] Kapalı (Narrative:PrewarmEnabled=false).");
                return;
            }

            _logger.LogInformation("[PREWARM] AI anlatı ısıtma başladı.");
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[PREWARM] Isıtma turu başarısız."); }

                try { await Task.Delay(Cycle, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Public: admin/manuel doğrulama için. Isıtılan maç sayısını döner.</summary>
        public async Task<int> RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matches = sp.GetRequiredService<IMatchReadRepository>();
            var store   = sp.GetRequiredService<IRadarNarrativeStore>();

            var now = DateTime.UtcNow;

            // Repository KİLİTLİ KAPSAMI zaten uyguluyor (Coverage:LeagueAllowList).
            var candidates = matches
                .GetUpcomingMatches(now, now.AddHours(HorizonHours))
                .Where(m => m.HomeTeamId > 0 && m.AwayTeamId > 0)
                .OrderBy(m => m.MatchDate)          // en yakın maç önce
                .ToList();

            var due = candidates.Where(m => NeedsWarm(m.Id, m.MatchDate, now, store))
                                .Take(MaxMatchesPerCycle)
                                .ToList();

            if (due.Count == 0)
            {
                _logger.LogInformation("[PREWARM] Isıtılacak maç yok (aday={Candidates}).", candidates.Count);
                return 0;
            }

            var warmed = 0;
            foreach (var match in due)
            {
                if (ct.IsCancellationRequested) break;

                // Her maç KENDİ scope'unda — biri hata verirse diğerleri etkilenmez.
                try
                {
                    using var matchScope = _scopeFactory.CreateScope();
                    var useCase = matchScope.ServiceProvider.GetRequiredService<GetMatchDetailAIContextUseCase>();

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(PerMatchTimeout);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // AYNI use case, AYNI zincir. Anlatı burada üretilip cache'e yazılır;
                    // kullanıcının /detail isteği artık LLM beklemez.
                    await useCase.ExecuteAsync(match.Id, timeout.Token);

                    _lastWarmed[match.Id] = DateTime.UtcNow;
                    warmed++;
                    _logger.LogInformation(
                        "[PREWARM] match={MatchId} ısıtıldı ({Elapsed}ms).", match.Id, sw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Üretim başarısız → uygulama etkilenmez, fallback davranışı korunur.
                    // Sonsuz retry yok: bir sonraki turda sırası gelirse tekrar denenir.
                    _logger.LogWarning(ex, "[PREWARM] match={MatchId} ısıtılamadı.", match.Id);
                }
            }

            _logger.LogInformation("[PREWARM] Tur bitti: {Warmed}/{Due} maç ısıtıldı.", warmed, due.Count);
            return warmed;
        }

        /// <summary>
        /// Bu maç ısıtılmalı mı?
        ///  • Hiç snapshot yoksa → EVET (kullanıcı gelirse LLM bekler).
        ///  • Son ısıtmanın üzerinden yeterli süre geçtiyse → EVET (bağlam değişmiş olabilir;
        ///    değişmediyse pipeline'ın context-hash cache'i zaten LLM çağırmaz).
        ///  • Aksi hâlde → HAYIR (gereksiz iş yok).
        /// </summary>
        private bool NeedsWarm(int matchId, DateTime kickoffUtc, DateTime now, IRadarNarrativeStore store)
        {
            var hasSnapshot =
                store.TryGetLatestForMatch(RadarSurface.MatchDetail, matchId, out _) &&
                store.TryGetLatestForMatch(RadarSurface.Discover, matchId, out _) &&
                store.TryGetLatestForMatch(RadarSurface.AiIncele, matchId, out _);

            if (!hasSnapshot) return true;

            if (!_lastWarmed.TryGetValue(matchId, out var last)) return true;

            var inFinalApproach = kickoffUtc - now <= TimeSpan.FromHours(FinalApproachHours);
            var interval = inFinalApproach ? FinalApproachRewarmAfter : RewarmAfter;

            return now - last >= interval;
        }
    }
}
