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

        // 🚀 FORMAX GDP — Engine Integration Pipeline (uçtan uca; tüm aşamalar tek akışta)
        services.AddGlobalDataPipeline();

        // ── Sprint 1: Lineup repositories ────────────────────────────────────
        services.AddScoped<IMatchLineupRepository, MatchLineupRepository>();
        services.AddScoped<IMatchPlayerStatusRepository, MatchPlayerStatusRepository>();

        // ── Sprint 2: Standings & competition context repositories ────────────
        services.AddScoped<ILeagueStandingRepository, LeagueStandingRepository>();
        services.AddScoped<ICompetitionContextRepository, CompetitionContextRepository>();
        services.AddScoped<ILeagueExternalMappingRepository, LeagueExternalMappingRepository>();

        // ── Sprint 3: Live match intelligence repositories ────────────────────
        services.AddScoped<IMatchLiveStatsRepository, MatchLiveStatsRepository>();
        services.AddScoped<IMatchMomentumRepository, MatchMomentumRepository>();
        services.AddScoped<IMatchLiveEventIngestionRepository, MatchLiveEventIngestionRepository>();

        // ── Sprint 3b: Distributed ingestion lock ─────────────────────────────
        services.AddScoped<ILiveIngestionLockRepository, LiveIngestionLockRepository>();

        // ── Sprint 0: Fixture sync ─────────────────────────────────────────────
        services.AddScoped<IFixtureSyncRepository, FixtureSyncRepository>();
        services.AddScoped<IFixtureSyncLockRepository, FixtureSyncLockRepository>();

        // ── Sprint 4: NABIZ feed intelligence ─────────────────────────────────
        services.AddScoped<INabizFeedRepository, NabizFeedRepository>();
        services.AddSingleton<NabizRelevanceEngine>();
        services.AddScoped<INabizFeedFetcher, NabizRssFeedFetcher>();

        return services;
    }
}