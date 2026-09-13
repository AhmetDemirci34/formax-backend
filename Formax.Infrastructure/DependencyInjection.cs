using Formax.Application.AI.Learning;
using Formax.Application.AI.Recommendation;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.Services.Nabiz;
using Formax.Infrastructure.Nabiz;
using Formax.Application.Services;
using Formax.Application.Services.AdminDashboard;
using Formax.Application.Services.AI;
using Formax.Application.Services.Feed;
using Formax.Application.Services.Intelligence;
using Formax.Application.Services.Recommendation;
using Formax.Application.UseCases.Live;
using Formax.Engine.Core.EdgeScoreEngine;
using Formax.Engine.Core.ExternalTrends;
using Formax.Engine.Core.GlobalTrends;
using Formax.Engine.Core.MatchEngine;
using Formax.Engine.Core.Personality;
using Formax.Engine.Core.ProbabilityEngine;
using Formax.Engine.Core.ScoreEngine;
using Formax.Engine.Core.UserIntelligenceEngine;
using Formax.Infrastructure.Conflict;
using Formax.Infrastructure.Coverage;
using Formax.Infrastructure.MatchIdentity;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize;
using Formax.Infrastructure.Pipeline;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Scheduling;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Services.Recommendation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using Formax.Engine.Core.Scoring;


namespace Formax.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IFeedInteractionRepository, FeedInteractionRepository>();
        services.AddScoped<IMatchRewardStatsRepository, MatchRewardStatsRepository>();
        services.AddScoped<IFirstSessionFeedService, FirstSessionFeedService>();
        services.AddScoped<GetLiveMatchReadingUseCase>();

        services.AddScoped<UserEmbeddingBuilder>();
        services.AddScoped<MatchEmbeddingBuilder>();

        services.AddScoped<IFeedEventPublisher, FeedEventPublisher>();
        services.AddScoped<AffinityScorer>();
        services.AddScoped<UserWeightLearningService>();
        services.AddScoped<FeedRankingPipeline>();

        services.AddScoped<IUserInterestService, UserInterestService>();

        services.AddScoped<IFeedScoreLogRepository, FeedScoreLogRepository>();
        services.AddScoped<FeedLogService>();

        services.AddScoped<WeightConfigService>();
        services.AddScoped<IAIWeightConfigRepository, AIWeightConfigRepository>();

        services.AddScoped<AnalyticsQueryService>();

        services.AddScoped<AdminAiDashboardAssembler>();
        services.AddScoped<AdminAiDashboardBuilder>();

        services.AddScoped<IAiStateMetricsReadRepository, AiStateMetricsReadRepository>();
        services.AddScoped<AdminAiInsightEngine>();

        services.AddScoped<ITrendService, TrendService>();
        services.AddScoped<IRewardEventProcessor, RewardEventProcessor>();

        services.AddScoped<IMatchRewardStatsRepository, MatchRewardStatsRepository>();

        services.AddScoped<ITeamReadRepository, TeamReadRepository>();

        services.AddScoped<IUserPickStatsRepository, UserPickStatsRepository>();

        // 🔥 FIXLER
        services.AddScoped<ExplainBuilder>();
        services.AddScoped<UserPickStatsService>();

        services.AddScoped<BanditDiscoveryService>();

        services.AddScoped<MatchEngine>();
        services.AddScoped<ProbabilityEngine>();
        services.AddScoped<EdgeScoreEngine>();
        services.AddScoped<UserIntelligenceEngine>();
        services.AddScoped<ScoreEngine>();
        services.AddScoped<GlobalTrendEngine>();
        services.AddSingleton<ExternalTrendCache>();
        services.AddScoped<ExternalTrendEngine>();
        services.AddScoped<PersonalityEngine>();
        services.AddScoped<IUserTasteProfileRepository, UserTasteProfileRepository>();
        services.AddScoped<UserTasteEngine>();
        services.AddScoped<TrendDeltaService>();
        services.AddScoped<GlobalTrendService>();
        services.AddScoped<GlobalScoreService>();
        services.AddScoped<FeedIntelligenceService>();
        services.AddScoped<UserBehaviorService>();
        services.AddScoped<ExternalTrendService>();

        // 🌐 FORMAX GDP — Provider Engine (iskelet; somut sağlayıcılar henüz yok)
        services.AddProviderEngine();

        // ⏱️ FORMAX GDP — Provider Scheduler (merkezi zamanlama; timer/BackgroundService henüz yok)
        services.AddProviderScheduler();

        // ❤️ FORMAX GDP — Provider Health (merkezi sağlık takibi; health algoritması henüz yok)
        services.AddProviderHealth();

        // ⚙️ FORMAX GDP — Provider Configuration (merkezi ayar; gerçek config yükleme henüz yok)
        services.AddProviderConfiguration();

        // 🔄 FORMAX GDP — Normalize Engine (iskelet; eşleştirme sonraki fazda)
        services.AddNormalizeEngine();

        // 🧩 FORMAX GDP — Match Identity Engine (iskelet; eşleştirme stratejileri sonraki fazda)
        services.AddMatchIdentityEngine();

        // 🔗 FORMAX GDP — Merge Engine (iskelet; merge stratejileri sonraki fazda)
        services.AddMergeEngine();

        // ⚖️ FORMAX GDP — Conflict Engine (iskelet; çözüm kriterleri sonraki fazda)
        services.AddConflictEngine();

        // 📊 FORMAX GDP — Coverage Engine (iskelet; kategori analizörleri sonraki fazda)
        services.AddCoverageEngine();

        // 🧭 FORMAX GDP — Match Context Resolver (gerçek Match → takım/lig/koordinat; sabit değer yok)
        services.AddScoped<Providers.Context.IMatchContextResolver, Providers.Context.MatchContextResolver>();

        // 💾 FORMAX GDP — Database Persist (son MergeResult'u kalıcılaştırır)
        services.AddScoped<Persistence.IGdpMatchPersister, Persistence.GdpMatchPersister>();
        services.AddScoped<Persistence.IGdpWeatherPersister, Persistence.GdpWeatherPersister>();
        services.AddScoped<Persistence.H2H.IGdpH2HPersister, Persistence.H2H.GdpH2HPersister>();
        services.AddScoped<Persistence.Standings.IGdpStandingsPersister, Persistence.Standings.GdpStandingsPersister>();

        // 🏛️ FORMAX Historical Data Platform — CSV import motoru
        services.AddScoped<Historical.IHistoricalImportService, Historical.HistoricalImportService>();
        services.AddScoped<Historical.Features.IHistoricalFeatureService, Historical.Features.HistoricalFeatureService>();
        services.AddScoped<Historical.Features.IFeatureStoreBuilder, Historical.Features.FeatureStoreBuilder>();
        services.AddScoped<Historical.Features.IFeatureStoreReader, Historical.Features.FeatureStoreReader>();
        services.AddScoped<Historical.Dataset.IDatasetBuilder, Historical.Dataset.DatasetBuilder>();
        services.AddScoped<Historical.Dataset.IDatasetValidator, Historical.Dataset.DatasetValidator>();
        // 🔮 Probability Engine — FAZ 3.1 Feature Loader (yalnız Feature Store'dan okur; Historical'a erişmez)
        services.AddScoped<Historical.Prediction.IFeatureLoader, Historical.Prediction.FeatureLoader>();
        // 🔮 Probability Engine — FAZ 3.2 Model Training (yalnız Dataset v1; deterministik softmax)
        services.AddScoped<Historical.Prediction.ModelTrainer>();
        services.AddScoped<Historical.Prediction.IModelTrainer>(sp => sp.GetRequiredService<Historical.Prediction.ModelTrainer>());
        services.AddSingleton<Historical.Prediction.IModelStore, Historical.Prediction.FileModelStore>();
        // 🔮 Probability Engine — FAZ 3.3 Model Selection (genişletilebilir: yeni model = yeni IModelCandidate)
        services.AddScoped<Historical.Prediction.Selection.IModelCandidate, Historical.Prediction.Selection.Candidates.LogisticRegressionCandidate>();
        services.AddScoped<Historical.Prediction.Selection.IModelCandidate, Historical.Prediction.Selection.Candidates.LightGbmCandidate>();
        services.AddScoped<Historical.Prediction.Selection.IModelSelector, Historical.Prediction.Selection.ModelSelector>();
        // 🔮 Probability Engine — FAZ 3.4 Ensemble (soft-voting; LR+LightGBM, deterministik ağırlık; XGBoost/CatBoost aynı yapıya eklenebilir)
        services.AddScoped<Historical.Prediction.Ensemble.IEnsembleBuilder, Historical.Prediction.Ensemble.EnsembleBuilder>();
        services.AddScoped<Historical.Prediction.Ensemble.IEnsembleStore, Historical.Prediction.Ensemble.FileEnsembleStore>();
        // 🔮 Probability Engine — FAZ 3.5 Confidence Engine (probability'den AYRI; calibration + reliability + level)
        services.AddScoped<Historical.Prediction.Confidence.IConfidenceCalibrator, Historical.Prediction.Confidence.ConfidenceCalibrator>();
        services.AddSingleton<Historical.Prediction.Confidence.IConfidenceCalibrationStore, Historical.Prediction.Confidence.FileConfidenceCalibrationStore>();
        // 🔮 Probability Engine — FAZ 3.6 Explainability (occlusion/baseline-ablation; ensemble kara-kutu, deterministik)
        services.AddScoped<Historical.Prediction.Explainability.IExplainerBuilder, Historical.Prediction.Explainability.ExplainerBuilder>();
        services.AddSingleton<Historical.Prediction.Explainability.IExplainerStore, Historical.Prediction.Explainability.FileExplainerStore>();
        // 🔮 Probability Engine — FAZ 3.8 Model Registry (versiyonlama, aktif model, rollback)
        services.AddSingleton<Historical.Prediction.Registry.IModelRegistry, Historical.Prediction.Registry.FileModelRegistry>();
        // 🔮 Probability Engine — FAZ 3.7 Prediction API (MatchId → Probability + Confidence + Explainability tek response)
        services.AddSingleton<Historical.Prediction.Api.IPredictiveModelProvider, Historical.Prediction.Api.PredictiveModelProvider>();
        services.AddScoped<Historical.Prediction.Api.IMatchPredictionService, Historical.Prediction.Api.MatchPredictionService>();
        // 🛰️ FAZ 4.1 Match Ranking Engine — Radar Score ("Bugün hangi maçı izlemeliyim?"). Config-driven ağırlıklar.
        // Ağırlıklar config'den bağlanabilir: host'ta AddSingleton(config.GetSection("ProbabilityEngine:RadarWeights").Get<RadarWeights>() ?? RadarWeights.Balanced).
        services.AddSingleton(Historical.Prediction.Ranking.RadarWeights.Balanced);
        services.AddSingleton<Historical.Prediction.Ranking.IRadarScoreEngine, Historical.Prediction.Ranking.RadarScoreEngine>();
        services.AddScoped<Historical.Prediction.Ranking.IRadarInputBuilder, Historical.Prediction.Ranking.RadarInputBuilder>();
        services.AddScoped<Historical.Prediction.Ranking.IMatchRankingService, Historical.Prediction.Ranking.MatchRankingService>();
        // 🛰️ FAZ 4.2 User Interest Integration — Base Radar Score + User Interest = Personal Radar Score.
        // Mevcut UserInterestEngine/MatchAffinityEngine tüketilir (yeniden yazılmaz); Radar Score Engine'e dokunulmaz.
        services.AddSingleton(Historical.Prediction.Ranking.Personalization.PersonalRadarWeights.Default);
        services.AddSingleton<Historical.Prediction.Ranking.Personalization.IPersonalRadarScoreEngine, Historical.Prediction.Ranking.Personalization.PersonalRadarScoreEngine>();
        services.AddScoped<Historical.Prediction.Ranking.Personalization.IUserInterestSignalProvider, Historical.Prediction.Ranking.Personalization.AffinityUserInterestSignalProvider>();
        services.AddScoped<Historical.Prediction.Ranking.Personalization.IPersonalMatchRankingService, Historical.Prediction.Ranking.Personalization.PersonalMatchRankingService>();
        // 🛰️ FAZ 4.3 Discovery Engine — Radar Engine'in ÜST katmanı: en değerli + en ÇEŞİTLİ feed (Radar Score değişmez).
        services.AddSingleton(Historical.Prediction.Ranking.Discovery.DiscoveryWeights.Default);
        services.AddSingleton<Historical.Prediction.Ranking.Discovery.IDiscoveryEngine, Historical.Prediction.Ranking.Discovery.DiscoveryEngine>();
        services.AddScoped<Historical.Prediction.Ranking.Discovery.IDiscoveryFeedService, Historical.Prediction.Ranking.Discovery.DiscoveryFeedService>();
        // 🛰️ FAZ 4.4 Recommendation Engine — Discovery Feed'in ÜST açıklama katmanı ("neden öneriyorum?"). Sıralamayı DEĞİŞTİRMEZ.
        services.AddSingleton(Historical.Prediction.Ranking.Recommendation.RecommendationWeights.Default);
        services.AddSingleton<Historical.Prediction.Ranking.Recommendation.IMatchRecommendationEngine, Historical.Prediction.Ranking.Recommendation.MatchRecommendationEngine>();
        services.AddScoped<Historical.Prediction.Ranking.Recommendation.IMatchRecommendationService, Historical.Prediction.Ranking.Recommendation.MatchRecommendationService>();
        // 🛰️ Radar üst katmanları için paylaşılan bağlam kurucu (Discovery + Radar breakdown; DRY)
        services.AddScoped<Historical.Prediction.Ranking.IRadarContextBuilder, Historical.Prediction.Ranking.RadarContextBuilder>();
        // 🛰️ FAZ 4.5 Hidden Gems — bağımsız analiz katmanı (sıralama/rozet değil; kalite × obscurity)
        services.AddSingleton(Historical.Prediction.Ranking.HiddenGems.HiddenGemWeights.Default);
        services.AddSingleton<Historical.Prediction.Ranking.HiddenGems.IHiddenGemsEngine, Historical.Prediction.Ranking.HiddenGems.HiddenGemsEngine>();
        services.AddScoped<Historical.Prediction.Ranking.HiddenGems.IHiddenGemsService, Historical.Prediction.Ranking.HiddenGems.HiddenGemsService>();
        // 🛰️ FAZ 4.6 Daily Picks — günün en iyi maç listesi (seçer; sıralamayı bozmaz)
        services.AddScoped<Historical.Prediction.Ranking.DailyPicks.IDailyPicksService, Historical.Prediction.Ranking.DailyPicks.DailyPicksService>();
        // 🛰️ FAZ 4.7 Radar API — tüm görünümleri birleştiren sorgu servisi
        services.AddScoped<Historical.Prediction.Ranking.Api.IRadarQueryService, Historical.Prediction.Ranking.Api.RadarQueryService>();

        // 🚀 FORMAX GDP — Engine Integration Pipeline (uçtan uca; tüm aşamalar tek akışta)
        services.AddGlobalDataPipeline();

        // ── Sprint 1: Lineup repositories ────────────────────────────────────
        services.AddScoped<IMatchLineupRepository, MatchLineupRepository>();
        services.AddScoped<IMatchPlayerStatusRepository, MatchPlayerStatusRepository>();

        // ── Sprint 2: Standings & competition context repositories ────────────
        services.AddScoped<ILeagueStandingRepository, LeagueStandingRepository>();
        services.AddScoped<ICompetitionContextRepository, CompetitionContextRepository>();
        // GDP League Coverage Intelligence — coverage'ı gerçek DB'den hesaplar (yeni API yok).
        services.AddScoped<Formax.Application.Interfaces.ILeagueCoverageService,
            Formax.Infrastructure.Coverage.LeagueCoverageService>();
        // Timeline Operations & Coverage — salt-okunur, cache'li operasyonel dashboard (gerçek veri).
        services.AddScoped<Formax.Application.Interfaces.ITimelineCoverageService,
            Formax.Infrastructure.Coverage.TimelineCoverageService>();
        services.AddScoped<ILeagueExternalMappingRepository, LeagueExternalMappingRepository>();

        // ── Phase 6: api-football team season statistics repository ───────────
        services.AddScoped<ITeamSeasonStatisticRepository, TeamSeasonStatisticRepository>();

        // ── Phase 6 / Slice 2: api-football match prediction repository (AI-only) ──
        services.AddScoped<IMatchPredictionSignalRepository, MatchPredictionSignalRepository>();

        // ── Phase 6 Final: api-football team profile repository (AI-only) ──────
        services.AddScoped<ITeamProfileSignalRepository, TeamProfileSignalRepository>();
        // Football Intelligence v1.0 — player/squad intelligence repo + ingestion service.
        services.AddScoped<Formax.Application.Interfaces.ITeamPlayerIntelligenceRepository,
            Formax.Infrastructure.Repositories.TeamPlayerIntelligenceRepository>();
        services.AddScoped<Formax.Infrastructure.Services.PlayerIntelligenceIngestionService>();

        // ── Phase 7: Social Discovery repositories (Canonical Social) ──────────
        services.AddScoped<IOfficialSocialAccountRepository, OfficialSocialAccountRepository>();
        services.AddScoped<ISocialPostRepository, SocialPostRepository>();

        // ── Sprint 3: Live match intelligence repositories ────────────────────
        services.AddScoped<IMatchLiveStatsRepository, MatchLiveStatsRepository>();
        // Bitmiş maçın KANONİK olay/istatistik okuyucusu — salt DB, sağlayıcıya çıkmaz.
        services.AddScoped<IPostMatchDataReader, Formax.Infrastructure.PostMatch.PostMatchDataReader>();
        services.AddScoped<IMatchMomentumRepository, MatchMomentumRepository>();
        services.AddScoped<IMatchLiveEventIngestionRepository, MatchLiveEventIngestionRepository>();

        // ── Sprint 3b: Distributed ingestion lock ─────────────────────────────
        services.AddScoped<ILiveIngestionLockRepository, LiveIngestionLockRepository>();

        // ── Sprint 0: Fixture sync ─────────────────────────────────────────────
        services.AddScoped<IFixtureSyncRepository, FixtureSyncRepository>();

        // SONUCLAR SEKMESI — gun bazli bitmis mac okuma yolu (salt DB, sifir dis istek).
        services.AddScoped<IMatchResultsReader, MatchResultsReader>();

        // ── MAÇ SONRASI VİDEO ──────────────────────────────────────────────────
        // Bitmiş maç ekranı YALNIZ videodan beslenir. Maç sonrası HABER eşleştirmesi
        // (02.09.2026 ürün kararı) kaldırılmıştır: ne toplayan job vardır, ne okuyan uç.
        // MatchPostContentLinks tablosu ve geçmiş satırları silinmemiştir; yalnız
        // beslenmez ve okunmaz.
        services.AddHttpClient("postmatch-video", c =>
        {
            // Bu istemci YALNIZ resmî video uçlarına (YouTube kanal akışı + oembed) gider.
            // api-football istemcisiyle karışmaz: video araması futbol veri kotasına
            // dokunmamalıdır.
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("FormaxPostMatchVideo/1.0");
        });
        services.AddScoped<IMatchVideoReader, Formax.Infrastructure.PostMatch.MatchVideoReader>();
        services.AddScoped<IVideoEmbedVerifier, Formax.Infrastructure.PostMatch.YouTubeEmbedVerifier>();
        services.AddScoped<IMatchVideoRegistrar, Formax.Infrastructure.PostMatch.MatchVideoRegistrar>();
        services.AddScoped<Formax.Infrastructure.PostMatch.MatchVideoAuditService>();
        // ── RESMÎ VİDEO KEŞİF ZİNCİRİ ─────────────────────────────────────────
        //
        // Tek sağlayıcı yerine ÖNCELİKLİ ZİNCİR (07.09.2026): keşfin tamamı YouTube
        // RSS akışının son ~15 videosuna bağlıydı; akıştan düşen resmî özet kalıcı
        // olarak kayboluyordu. Zincir hak sahipliği sırasına göre çalışır ve
        // yapılandırılmamış sağlayıcıyı SESSİZCE atlar (NotConfigured).
        //
        // Sağlayıcılar zincire ÜYE olarak kaydedilir; dışarıya açılan tek
        // IOfficialMatchVideoProvider zincirin kendisidir.
        services.AddScoped<Formax.Infrastructure.Picks.UserPickSettlementService>();
        services.AddScoped<Formax.Infrastructure.PostMatch.OfficialSiteFeedVideoProvider>();
        services.AddScoped<Formax.Infrastructure.PostMatch.YouTubeDataApiVideoProvider>();
        services.AddScoped<Formax.Infrastructure.PostMatch.YouTubeChannelFeedVideoProvider>();
        services.AddScoped<IOfficialMatchVideoProvider>(sp =>
        {
            // "Disabled" tek anahtarla tüm keşfi kapatır — sıfır dış istek.
            if (sp.GetRequiredService<IConfiguration>()
                  .GetValue("PostMatch:Video:Provider", "OfficialVideoChain") is "Disabled")
                return new Formax.Infrastructure.PostMatch.DisabledOfficialMatchVideoProvider();

            var members = new IOfficialMatchVideoProvider[]
            {
                sp.GetRequiredService<Formax.Infrastructure.PostMatch.OfficialSiteFeedVideoProvider>(),
                sp.GetRequiredService<Formax.Infrastructure.PostMatch.YouTubeDataApiVideoProvider>(),
                sp.GetRequiredService<Formax.Infrastructure.PostMatch.YouTubeChannelFeedVideoProvider>()
            };

            return new Formax.Infrastructure.PostMatch.CompositeOfficialMatchVideoProvider(
                members,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Formax.Infrastructure.PostMatch.CompositeOfficialMatchVideoProvider>>());
        });
        services.AddScoped<IFixtureSyncLockRepository, FixtureSyncLockRepository>();

        // ── RESMÎ KAYNAK ALTYAPISI ───────────────────────────────────────────
        // Kadro / sonuç / olay / istatistik / kritik gelişme için TEK ortak indirici.
        // Güvenlik: yalnız HTTPS + kayıt defterindeki doğrulanmış host'lar; yönlendirme elle
        // izlenir ve her atlamada host yeniden doğrulanır; bağlantı anında özel/yerel IP
        // reddedilir (DNS yeniden bağlama koruması); çerez yok; gövde boyutu sınırlı.
        services.AddHttpClient(Formax.Infrastructure.OfficialSources.OfficialContentFetcher.HttpClientName, c =>
            {
                c.Timeout = System.Threading.Timeout.InfiniteTimeSpan; // süreyi fetcher yönetir
            })
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ConnectCallback = Formax.Infrastructure.OfficialSources.OfficialNetworkGuard.ConnectGuardedAsync
            });
        services.AddSingleton<Formax.Infrastructure.OfficialSources.OfficialHostRateLimiter>();
        services.AddSingleton<Formax.Infrastructure.OfficialSources.IOfficialAddressResolver,
            Formax.Infrastructure.OfficialSources.DnsOfficialAddressResolver>();
        services.AddSingleton(new Formax.Infrastructure.OfficialSources.OfficialFetcherOptions());
        services.AddScoped<Formax.Infrastructure.OfficialSources.OfficialSourceStore>();
        services.AddScoped<Formax.Application.Services.OfficialSources.IOfficialContentFetcher,
            Formax.Infrastructure.OfficialSources.OfficialContentFetcher>();
        // Kaynak türüne özel parser'lar — yalnız canlı doğrulanmış kaynaklar kayıtlıdır.
        services.AddScoped<Formax.Application.Services.OfficialSources.IOfficialCompetitionSource,
            Formax.Infrastructure.OfficialSources.Providers.SerieASdpSource>();
        services.AddScoped<Formax.Application.Services.OfficialSources.IOfficialCompetitionSource,
            Formax.Infrastructure.OfficialSources.Providers.PremierLeagueSdpSource>();
        services.AddScoped<Formax.Application.Services.OfficialSources.IOfficialCompetitionSource,
            Formax.Infrastructure.OfficialSources.Providers.TffSource>();
        services.AddScoped<Formax.Infrastructure.OfficialSources.OfficialLineupCollector>();
        // Maç bildirimi — mevcut UserNotification + INotificationService üzerinden, tekil anahtarlı.
        services.AddScoped<IMatchNotificationDispatcher, Formax.Infrastructure.Notifications.MatchNotificationDispatcher>();

        // ── AI MAÇ ANALİZİ — okuma (kullanıcı yolu, salt DB) ve üretim (yalnız arka plan) ──
        services.AddScoped<Formax.Application.Services.MatchAnalysis.IMatchAnalysisReader,
            Formax.Infrastructure.MatchAnalysis.MatchAnalysisReader>();
        services.AddScoped<Formax.Infrastructure.MatchAnalysis.MatchAnalysisGenerator>();

        // ── Sprint 4: NABIZ feed intelligence ─────────────────────────────────
        services.AddScoped<INabizFeedRepository, NabizFeedRepository>();
        services.AddSingleton<NabizRelevanceEngine>();
        services.AddScoped<INabizFeedFetcher, NabizRssFeedFetcher>();

        return services;
    }
}