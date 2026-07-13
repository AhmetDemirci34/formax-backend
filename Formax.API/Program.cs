using Formax.API.Middleware;
using Formax.Application.Abstractions;
using Formax.Application.AI.Audit;
using Formax.Application.AI.Confidence;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Guardrails;
using Formax.Application.AI.Learning;
using Formax.Application.AI.Limits;
using Formax.Application.AI.LLM;
using Formax.Application.AI.LLM.Prompt;
using Formax.Application.AI.LLM.Services;
using Formax.Application.AI.Memory;
using Formax.Application.AI.Narrative;
using Formax.Application.AI.Narrative.Depth;
using Formax.Application.AI.Narrative.Home;
using Formax.Application.AI.Recommendation;
using Formax.Application.AI.SelfAudit;
using Formax.Application.AI.World;
using Formax.Application.Common.Options;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.FaiOverview;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.Services;
using Formax.Application.Services.AdminDashboard;
using Formax.Application.Services.AI;
using Formax.Application.Services.Backtest;
using Formax.Application.Services.Discovery;
using Formax.Application.Services.FaiOverview;
using Formax.Application.Services.Feed;
using Formax.Application.Services.Home;
using Formax.Application.Services.Intelligence;
using Formax.Application.Services.Interest;
using Formax.Application.Services.Narrative;
using Formax.Application.Services.Radar;
using Formax.Application.Services.Recommendation;
using Formax.Application.Services.Sapma;
using Formax.Application.Services.Signals;
using Formax.Application.States;
using Formax.Application.States.Home;
using Formax.Application.UseCases;
using Formax.Application.UseCases.Admin;
using Formax.Application.UseCases.Auth;
using Formax.Application.UseCases.Coupons;
using Formax.Application.UseCases.Follow;
using Formax.Application.UseCases.Home;
using Formax.Application.UseCases.Interest;
using Formax.Application.UseCases.Live;
using Formax.Application.UseCases.Media;
using Formax.Application.UseCases.Notifications;
using Formax.Application.UseCases.Teams;
using Formax.Engine;
using Formax.Engine.Core.ExternalTrends;
using Formax.Engine.Core.Trends;
using Formax.Infrastructure;
using Formax.Infrastructure.AI.Audit;
using Formax.Infrastructure.AI.LLM;
using Formax.Infrastructure.AI.SelfAudit;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Nabiz;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Live;
using Formax.Infrastructure.Personalization;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.ReadProviders;
using Formax.Infrastructure.Repositories;
using Formax.Infrastructure.Services;
using Formax.Infrastructure.Services.Recommendation;
using Formax.Infrastructure.States;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Linq;
using System.Text;





internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5063");

        // 🔥 SADECE BUNU EKLEDİM
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

        builder.Services.AddScoped<IMatchOynanmaSnapshotWriter, MatchOynanmaSnapshotWriter>();

        // ---------------- SWAGGER ----------------
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Bearer token",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            c.AddSecurityDefinition("Bearer", securityScheme);

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
        { securityScheme, Array.Empty<string>() }
            });
        });

        // ---------------- CORS ----------------
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend",
                policy =>
                {
                    policy
                        .WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        });

        // ---------------- DB ----------------
        builder.Services.AddDbContext<FormaxDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("FormaxDB"));
        });

        builder.Services.AddInfrastructure();

        // ---------------- OPTIONS ----------------
        builder.Services.Configure<AIBehaviorOptions>(
            builder.Configuration.GetSection("AI"));

        // ---------------- JWT AUTH ----------------
        var jwtKey = builder.Configuration["Jwt:Key"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new Exception("Jwt Key missing");

        builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader) &&
                        authHeader.StartsWith("Bearer "))
                    {
                        context.Token = authHeader.Substring("Bearer ".Length).Trim();
                    }

                    return Task.CompletedTask;
                }
            };

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        builder.Services.AddAuthorization();


        // ---------------- REPOSITORIES ----------------

        builder.Services.AddHttpClient<IAIService, OpenAIService>();

        builder.Services.AddSingleton<CommentEngine>();

        builder.Services.AddSingleton<DetailAnalysisEngine>();

        builder.Services.AddSingleton<UserToneEngine>();

        builder.Services.AddScoped<UserLearningService>();

        builder.Services.AddSingleton<AdaptiveRankingService>();

        builder.Services.AddScoped<LabelEngine>();

        builder.Services.AddScoped<ExternalTrendService>();

        builder.Services.AddScoped<ExternalTrendEngine>();

        builder.Services.AddScoped<ExternalTrendCache>();

        builder.Services.AddScoped<RealtimeLearningOrchestrator>();

        builder.Services.AddScoped<TrendDecayEngine>();

        builder.Services.AddScoped<GlobalTrendService>();

        builder.Services.AddSingleton<BanditService>();

        builder.Services.AddSingleton<OddsMovementService>();

        builder.Services.AddScoped<IUserTasteVectorRepository, UserTasteVectorRepository>();

        builder.Services.AddScoped<IExternalTrendProvider, FakeExternalTrendProvider>();

        builder.Services.AddScoped<GetMatchDetailAIContextUseCase>();

        // Match Intelligence v2 (Görev #011) — mevcut MatchDetail pipeline'ını yeniden kullanan
        // paralel katman. Eski kayıtlar değişmez.
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.Lineup.IExpectedLineupEngine,
                                   Formax.Application.Services.MatchIntelligence.Lineup.ExpectedLineupEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.Lineup.ILivingLineupEngine,
                                   Formax.Application.Services.MatchIntelligence.Lineup.LivingLineupEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.News.INewsIntelligenceEngine,
                                   Formax.Application.Services.MatchIntelligence.News.NewsIntelligenceEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.Outlook.IMatchOutlookEngine,
                                   Formax.Application.Services.MatchIntelligence.Outlook.MatchOutlookEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.Market.IMarketIntelligenceEngine,
                                   Formax.Application.Services.MatchIntelligence.Market.MarketIntelligenceEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.WatchLive.IWatchLiveEngine,
                                   Formax.Application.Services.MatchIntelligence.WatchLive.WatchLiveEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.Live.ILiveIntelligenceEngine,
                                   Formax.Application.Services.MatchIntelligence.Live.LiveIntelligenceEngine>();
        builder.Services.AddScoped<Formax.Application.Services.MatchIntelligence.IMatchIntelligenceService,
                                   Formax.Application.Services.MatchIntelligence.MatchIntelligenceService>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchIntelligenceUseCase>();

        builder.Services.AddScoped<IOynanmaSinyalProvider, OynanmaSinyalProvider_Default>();

        builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        builder.Services.AddScoped<IMatchBanditRepository, MatchBanditRepository>();

        builder.Services.AddScoped<GetUserNotificationsUseCase>();

        builder.Services.AddScoped<IFaiOverviewService, FaiOverviewService>();

        builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();

        builder.Services.AddScoped<WorldExpectationService>();






        builder.Services.AddScoped<IMatchReadRepository, MatchReadRepository>();
        builder.Services.AddScoped<IMatchSapmaSnapshotRepository, MatchSapmaSnapshotRepository>();
        builder.Services.AddScoped<IMatchUpdateRepository, MatchUpdateRepository>();
        builder.Services.AddScoped<IMatchWriteRepository, MatchWriteRepository>();

        builder.Services.AddScoped<IUserPickStatsRepository, UserPickStatsRepository>();

        builder.Services.AddScoped<IUserConfidenceRepository, UserConfidenceRepository>();

        builder.Services.AddScoped<IMatchOynanmaSnapshotReadRepository, MatchOynanmaSnapshotReadRepository>();

        builder.Services.AddScoped<IMatchEventRepository, MatchEventRepository>();
        builder.Services.AddScoped<IMatchEventReadRepository, MatchEventReadRepository>();
        builder.Services.AddScoped<IMatchEventWriteRepository, MatchEventWriteRepository>();

        builder.Services.AddScoped<IUserStatsRepository, UserStatsRepository>();

        builder.Services.AddScoped<IFeedInteractionRepository, FeedInteractionRepository>();

        builder.Services.AddScoped<IPredictionTypeReadRepository, PredictionTypeReadRepository>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();

        builder.Services.AddScoped<IUserInterestEventRepository, UserInterestEventRepository>();


        builder.Services.AddScoped<IInterestEngine, InterestEngine>();
        builder.Services.AddScoped<IUserInterestTrackingService, UserInterestTrackingService>();

        builder.Services.AddScoped<IMatchInterestReadRepository, MatchInterestReadRepository>();

        builder.Services.AddScoped<ILastExtendedContextKeyRepository, LastExtendedContextKeyRepository>();

        builder.Services.AddScoped<IMatchRewardRepository, MatchRewardRepository>();

        builder.Services.AddScoped<IPreMatchReadProvider, PreMatchReadProvider>();

        builder.Services.AddScoped<ILiveMatchReadProvider, LiveMatchReadProvider>();

        builder.Services.AddScoped<ISquadReadProvider, SquadReadProvider>();

        builder.Services.AddScoped<ILiveMatchFeed, DummyLiveMatchFeed>();

        builder.Services.AddScoped<RewardEventProcessor>();

        builder.Services.AddScoped<IMatchRewardStatsRepository, MatchRewardStatsRepository>();

        builder.Services.AddScoped<IUserSessionInterestRepository, UserSessionInterestRepository>();

        builder.Services.AddScoped<IUserMatchFollowRepository, UserMatchFollowRepository>();
        builder.Services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();


        builder.Services.AddScoped<ContextualBanditService>();


        builder.Services.AddScoped<IUserWeightProfileRepository, UserWeightProfileRepository>();

        builder.Services.AddScoped<IUserInterestScoreRepository, UserInterestScoreRepository>();

        builder.Services.AddScoped<IFeedScoreLogRepository, FeedScoreLogRepository>();

        builder.Services.AddScoped<IUserPickRepository, UserPickRepository>();




        // ---------------- SAPMA ----------------

        builder.Services.AddScoped<ITeamReadRepository, TeamReadRepository>();

        builder.Services.AddSingleton<ILiveOynanmaSignalStore, InMemoryLiveOynanmaSignalStore>();

        builder.Services.AddScoped<OynanmaSinyalProvider_Default>();

        builder.Services.AddScoped<IOynanmaSinyalProvider, OynanmaSinyalProvider_LiveStoreFallback>();

        builder.Services.AddScoped<IGucSkoruCalculator, GucSkoruCalculator>();
        builder.Services.AddScoped<ISapmaMotor, SapmaMotor>();

        builder.Services.AddScoped<BacktestService>();
        builder.Services.AddScoped<PreMatchFinalBackfillService>();



        // ---------------- AI / STATE MACHINE ----------------

        builder.Services.AddScoped<IAIDecisionTraceWriter, AIDecisionTraceWriter>();
        builder.Services.AddScoped<IAISelfInvalidationLogWriter, AISelfInvalidationLogWriter>();
        builder.Services.AddScoped<IStateTransitionLogWriter, StateTransitionLogWriter>();

        builder.Services.AddScoped<AiGuardEvaluator>();
        builder.Services.AddScoped<AiForbiddenActionEvaluator>();
        builder.Services.AddScoped<AiForbiddenActionGuard>();

        // ❗ eksik zincir
        builder.Services.AddScoped<AiForbiddenActionIntegration>();
        builder.Services.AddScoped<AiForbiddenActionService>();

        builder.Services.AddScoped<IAIStateMachine, AIStateMachine>();

        builder.Services.AddScoped<AiDepthResolver>();
        builder.Services.AddScoped<AIUxStateResolver>();
        builder.Services.AddScoped<ContextDecayEvaluator>();


        // ---------------- AI NARRATIVE ----------------

        builder.Services.AddScoped<INarrativeToneResolver, NarrativeToneResolver>();
        builder.Services.AddScoped<IAINarrativeBuilder, AINarrativeBuilder>();
        builder.Services.AddScoped<AINarrativeService>();

        builder.Services.AddSingleton<AINarrativeUsageTracker>();


        // ---------------- HOME AI ----------------

        builder.Services.AddScoped<HomeAIStateResolver>();
        builder.Services.AddScoped<HomeNarrativeGuard>();

        builder.Services.AddScoped<AIContentDepthResolver>();

        builder.Services.AddScoped<IHomeNarrativeBuilder, HomeAIVitrineTextProvider>();

        builder.Services.AddScoped<AIHomeVitrineService>();

        builder.Services.AddScoped<GetHomeAIVitrineUseCase>();


        // ---------------- SERVICES ----------------

        builder.Services.AddScoped<MatchService>();

        builder.Services.AddScoped<IPlayedScoreCalculator, PlayedScoreCalculator>();

        builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();

        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();


        builder.Services.AddScoped<PlayRateService>();

        builder.Services.AddScoped<TrendDecayService>();

        builder.Services.AddScoped<GlobalTrendService>();

        builder.Services.AddScoped<TrendDeltaService>();


        builder.Services.AddScoped<ResultEngine>();

        builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();

        builder.Services.AddScoped<UserConfidenceService>();

        builder.Services.AddScoped<IInterestAggregationService, InterestAggregationService>();

        builder.Services.AddScoped<IUserInterestQueryService, UserInterestQueryService>();

        builder.Services.AddScoped<INotificationService, NullNotificationService>();

        builder.Services.AddScoped<UserExperienceTransferService>();

        builder.Services.AddScoped<IFirstSessionFeedService, FirstSessionFeedService>();

        builder.Services.AddScoped<IHomeInterestProfileService, HomeInterestProfileService>();

        builder.Services.AddScoped<LeagueBaselineService>();

        builder.Services.AddScoped<FeedLearningService>();

        builder.Services.AddScoped<SessionBehaviorService>();

        builder.Services.AddScoped<AdaptiveInterestLearningService>();

        builder.Services.AddScoped<SessionMemoryService>();

        builder.Services.AddScoped<TrendService>();

        builder.Services.AddScoped<MatchEventNotificationService>();


        builder.Services.AddSingleton<DiscoverySessionMemoryService>();

        builder.Services.AddScoped<UserWeightLearningService>();

        builder.Services.AddScoped<RecommendationStatService>();

        builder.Services.AddScoped<TrendWeightService>();

        builder.Services.AddScoped<IUserInterestService, UserInterestService>();

        builder.Services.AddScoped<BanditDecisionService>();

        builder.Services.AddScoped<FeedDiversityService>();

        builder.Services.AddScoped<FeedLogService>();

        builder.Services.AddScoped<IUserActionService, UserActionService>();

        builder.Services.AddScoped<IUserStatsService, UserStatsService>();
        builder.Services.AddScoped<ExplainBuilder>();
        builder.Services.AddScoped<UserPickStatsService>();
        builder.Services.AddScoped<RadarFilterService>();
        builder.Services.AddScoped<SmartSignalService>();
        builder.Services.AddScoped<UserLearningService>();

        builder.Services.AddScoped<UserTrendService>();

        builder.Services.AddScoped<IAppDbContext, FormaxDbContext>();

        builder.Services.AddScoped<ConfidenceCalculator>();

        builder.Services.AddScoped<IRecommendationService, RecommendationService>();
        builder.Services.AddScoped<ISessionService, SessionService>();
        builder.Services.AddScoped<IRewardProcessor, RewardProcessor>();

        builder.Services.AddScoped<IUserActionRepository, UserActionRepository>();




        // ---------------- USE CASES ----------------

        builder.Services.AddScoped<LoginUserUseCase>();
        builder.Services.AddScoped<RegisterUserUseCase>();

        builder.Services.AddScoped<GetMatchesUseCase>();

        builder.Services.AddScoped<TrackInterestEventUseCase>();

        builder.Services.AddScoped<GetRecommendationFeedUseCase>();

        builder.Services.AddScoped<GetHomeRadarUseCase>();

        builder.Services.AddScoped<GetLiveMatchReadingUseCase>();

        builder.Services.AddScoped<EmitMatchEventUseCase>();

        builder.Services.AddScoped<GetLiveMatchReadingUseCase>();
        builder.Services.AddScoped<EmitMatchEventUseCase>();

        builder.Services.AddScoped<MatchEventService>();

        builder.Services.AddScoped<GetUsersFollowingMatchUseCase>();
        builder.Services.AddScoped<GetFollowedMatchesUseCase>();
        builder.Services.AddScoped<FollowMatchUseCase>();
        builder.Services.AddScoped<UnfollowMatchUseCase>();


        builder.Services.AddScoped<NotificationFactory>();

        builder.Services.AddScoped<GetDailyAIFavoriteMatchesUseCase>();

        builder.Services.AddScoped<ResolveHomeAIStateUseCase>();

        builder.Services.AddScoped<GetDailyAIFavoriteMatchesUseCase>();
        builder.Services.AddScoped<ResolveHomeAIStateUseCase>();
        builder.Services.AddScoped<GetHomeRadarUseCase>();
        builder.Services.AddScoped<GetHomeNarrativeUseCase>();
        builder.Services.AddScoped<GetHomeLiveSignalsUseCase>();
        builder.Services.AddScoped<GetHomeTopSapmaUseCase>();

        builder.Services.AddScoped<GetTeamsUseCase>();
        builder.Services.AddScoped<GetMyTeamsUseCase>();


        builder.Services.AddScoped<RewardCalculator>();








        // ---------------- RADAR v2 — LLM REASONING CHAIN (audit'te ölü zincir) ----
        // ILLMClient → gerçek HTTP istemci (Ollama/OpenAI, yapılandırılmamışsa fallback).
        builder.Services.AddHttpClient<Formax.Application.AI.LLM.ILLMClient,
            Formax.Infrastructure.AI.LLM.HttpLLMClient>();
        builder.Services.AddScoped<Formax.Application.AI.LLM.Prompt.MatchNarrativePromptComposer>();
        builder.Services.AddScoped<Formax.Application.AI.LLM.Services.LLMNarrativeService>();

        // GetReadableMatchAnalysisUseCase ve eksik AI-analiz repo'ları (audit: kayıtsızdı).
        builder.Services.AddScoped<Formax.Application.Interfaces.IAIAnalysisReadRepository,
            Formax.Infrastructure.Repositories.AIAnalysisReadRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.Repositories.IAIAnalysisWriteRepository,
            Formax.Infrastructure.Repositories.AIAnalysisWriteRepository>();
        builder.Services.AddScoped<Formax.Application.UseCases.Media.GetReadableMatchAnalysisUseCase>();

        // RADAR v2 — Match Intelligence Context + Narrative Pipeline.
        builder.Services.AddScoped<Formax.Application.AI.Radar.MatchIntelligenceContextBuilder>();
        // RADAR v3 — Intelligence Reasoning Layer (FORMAX'ın beyni).
        builder.Services.AddScoped<Formax.Application.AI.Radar.Reasoning.ReasoningEngine>();

        // ---------------- DATA ENGINE v1 — Global Fixture Intelligence ----------------
        // Kimlik motorları stateless → singleton. Provider'lar HttpClient'lı, IFixtureProvider
        // koleksiyonu olarak çözülür (yeni kaynak = tek AddHttpClient satırı, ölçeklenebilir).
        builder.Services.AddSingleton<Formax.Application.Services.Fixtures.TeamIdentityResolver>();
        builder.Services.AddSingleton<Formax.Application.Services.Fixtures.LeagueIdentityResolver>();
        builder.Services.AddSingleton<Formax.Application.Services.Fixtures.FormaxMatchIdFactory>();
        builder.Services.AddSingleton<Formax.Application.Services.Fixtures.FixtureConfidenceEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Fixtures.FixtureDiscoveryService>();
        builder.Services.AddHttpClient<Formax.Application.Services.Fixtures.IFixtureProvider,
            Formax.Infrastructure.Fixtures.Providers.TheSportsDbFixtureProvider>();
        builder.Services.AddHttpClient<Formax.Application.Services.Fixtures.IFixtureProvider,
            Formax.Infrastructure.Fixtures.Providers.FootballDataOrgFixtureProvider>();
        // Fixtures kalıcılık + scheduler (başlangıç + her 6 saat, 30 gün keşif).
        builder.Services.AddScoped<Formax.Application.Interfaces.IFixtureRepository,
            Formax.Infrastructure.Repositories.FixtureRepository>();
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.FixtureDiscoveryJob>();

        // ---------------- DATA ENGINE v2 — Global News Discovery ----------------
        // Stateless motorlar singleton; provider'lar HttpClient'lı INewsProvider koleksiyonu.
        builder.Services.AddSingleton<Formax.Application.Services.News.Discovery.MatchNewsSearchQueryBuilder>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Discovery.NewsDuplicateDetector>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Discovery.NewsClusterEngine>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Discovery.NewsConfidenceEngine>();
        builder.Services.AddScoped<Formax.Application.Services.News.Discovery.GlobalNewsDiscoveryService>();
        builder.Services.AddScoped<Formax.Application.Interfaces.IMatchNewsRepository,
            Formax.Infrastructure.Repositories.MatchNewsRepository>();
        builder.Services.AddHttpClient<Formax.Application.Services.News.Discovery.INewsProvider,
            Formax.Infrastructure.News.Providers.GoogleNewsRssProvider>();
        builder.Services.AddHttpClient<Formax.Application.Services.News.Discovery.INewsProvider,
            Formax.Infrastructure.News.Providers.BingNewsRssProvider>();
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.NewsDiscoveryJob>();

        // ---------------- LIVE DATA ENGINE — Global Live Discovery ----------------
        // News Discovery ile aynı desen: açık kaynaklardan (RSS + genişletilebilir
        // resmi kulüp/federasyon/sosyal provider'ları) canlı skoru keşfeder ve mevcut
        // MatchLiveStats/MatchLiveEvents tablolarına yazar. LiveIntelligenceEngine bu
        // tabloları okur → motor değişmeden gerçek canlı veriyle beslenir. Keyless.
        builder.Services.AddSingleton<Formax.Application.Services.Live.Discovery.LiveSignalQueryBuilder>();
        builder.Services.AddSingleton<Formax.Application.Services.Live.Discovery.LiveScoreExtractor>();
        builder.Services.AddScoped<Formax.Application.Services.Live.Discovery.GlobalLiveDiscoveryService>();
        builder.Services.AddHttpClient<Formax.Application.Services.Live.Discovery.ILiveSignalProvider,
            Formax.Infrastructure.Live.Providers.RssLiveScoreProvider>();
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.LiveDiscoveryJob>();

        // ---------------- DATA ENGINE v2.1 — Match Intelligence Discovery -------------
        // Signal/Quality/Freshness motorları stateless → singleton. Evidence Store scoped.
        builder.Services.AddSingleton<Formax.Application.Services.News.Intelligence.SignalExtractor>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Intelligence.SourceQualityResolver>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Intelligence.FreshnessPolicy>();
        builder.Services.AddSingleton<Formax.Application.Services.News.Intelligence.MatchIntelligenceService>();
        builder.Services.AddScoped<Formax.Application.Interfaces.IMatchEvidenceRepository,
            Formax.Infrastructure.Repositories.MatchEvidenceRepository>();

        // ---------------- PLAYER INTELLIGENCE ENGINE (oyuncu-düzeyi; Radar'dan bağımsız) ----
        // Mevcut api-football entegrasyonunu genişletir; graceful fallback + IMemoryCache.
        builder.Services.AddHttpClient<Formax.Application.Interfaces.IPlayerStatsProvider,
            Formax.Infrastructure.Providers.ApiFootballPlayerStatsProvider>();
        builder.Services.AddScoped<Formax.Application.Interfaces.IPlayerIntelligenceEngine,
            Formax.Application.Services.Players.Intelligence.PlayerIntelligenceEngine>();

        // ---------------- HERO SELECTION ENGINE (skor okur, seçer; sinyal üretmez) ----
        // Ağırlıklar ayrı yapıdan (ileride config'e bağlanabilir). Narrative burada üretilmez.
        builder.Services.AddSingleton(Formax.Application.Services.Hero.HeroSelectionWeights.Default);
        builder.Services.AddScoped<Formax.Application.Interfaces.IHeroSelectionEngine,
            Formax.Application.Services.Hero.HeroSelectionEngine>();
        // HeroVisualIdentityEngine — takım kimliğinden görsel atmosfer (TeamColorProvider yerine).
        builder.Services.AddSingleton<Formax.Application.Interfaces.IHeroVisualIdentityEngine,
            Formax.Application.Services.Hero.VisualIdentity.HeroVisualIdentityEngine>();
        builder.Services.AddScoped<Formax.Application.AI.Radar.RadarPromptComposer>();
        builder.Services.AddScoped<Formax.Application.AI.Radar.RadarOutputGuard>();
        builder.Services.AddSingleton<Formax.Application.AI.Radar.IRadarNarrativeStore,
            Formax.Application.AI.Radar.InMemoryRadarNarrativeStore>();
        builder.Services.AddScoped<Formax.Application.AI.Radar.RadarNarrativePipeline>();

        // RADAR v2.2 — Dynamic Scenario Ranking (geniş market havuzu → en güçlü 3).
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Scenarios.MarketProbabilityEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Scenarios.ScenarioRankingService>();

        // ---------------- WORLD JOBS ----------------

        builder.Services.AddScoped<WorldPerceptionProvider>();

        // Register once as singleton, then add the hosted service from the same
        // instance — lets an admin endpoint resolve the job to trigger
        // RefreshStandingsAsync manually (test) without a second instance.
        builder.Services.AddSingleton<WorldPerceptionDailyJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorldPerceptionDailyJob>());
        builder.Services.AddHostedService<SapmaSnapshotJob>();

        // ── Sprint 0: Fixture sync ─────────────────────────────────────────────
        builder.Services.AddHostedService<FixtureSyncJob>();

        // Historical backfill (Sprint 19B) — singleton + hosted so an admin
        // endpoint can trigger RunCycleAsync manually for testing.
        builder.Services.AddSingleton<HistoricalSyncJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<HistoricalSyncJob>());

        // ── Memory Cache (IMemoryCache — used by ApiFootballSportsDataProvider) ──
        builder.Services.AddMemoryCache();

        // ── Sprint 20A+20B: API-Football H2H enrichment ──────────────────────
        builder.Services.AddHttpClient("ApiFootball");
        builder.Services.AddSingleton<Formax.Infrastructure.Cache.H2HCache>();
        builder.Services.AddScoped<Formax.Application.Interfaces.IH2HProvider,
            Formax.Infrastructure.Providers.ApiFootballH2HProvider>();

        // ── Sprint 1: Lineup engine ──────────────────────────────────────────
        // Active sports data provider. TheSportsDB free tier covers Türkiye Süper Lig
        // (fixtures, scores, standings) without an api-football key. To switch back to
        // api-football, swap the implementation type below (DTOs/jobs are unchanged).
        builder.Services.AddHttpClient<ISportsDataProvider, TheSportsDbProvider>();
        builder.Services.AddScoped<IMatchLineupRepository, MatchLineupRepository>();
        builder.Services.AddScoped<IMatchPlayerStatusRepository, MatchPlayerStatusRepository>();
        builder.Services.AddHostedService<LineupIngestionJob>();

        // ── Sprint 3: Live match intelligence ────────────────────────────────
        builder.Services.AddHostedService<LiveMatchIngestionJob>();

        // ── Sprint 4: NABIZ feed intelligence ─────────────────────────────────
        builder.Services.AddHostedService<NabizIngestionJob>();
        builder.Services.AddHttpClient("NabizRss", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Formax/1.0 (+https://formax.app)");
        });
        builder.Services.Configure<NabizOptions>(
            builder.Configuration.GetSection("Nabiz"));

        // ── R.8.1: Radar Source Engine — Source Registry ───────────────────────
        builder.Services.Configure<Formax.Infrastructure.Radar.Sources.RadarSourceOptions>(
            builder.Configuration.GetSection("Radar"));
        builder.Services.AddScoped<Formax.Application.Interfaces.ISourceDefinitionRepository,
            Formax.Infrastructure.Repositories.SourceDefinitionRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ISourceStatusRepository,
            Formax.Infrastructure.Repositories.SourceStatusRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ISourceRegistry,
            Formax.Application.Services.Radar.Sources.SourceRegistry>();
        builder.Services.AddScoped<Formax.Infrastructure.Radar.Sources.RadarSourceRegistryBootstrapper>();

        // ── R.8.2: Radar Source Scheduler (plan-only, no collection) ───────────
        builder.Services.Configure<Formax.Infrastructure.Radar.Sources.Scheduling.SchedulerOptions>(
            builder.Configuration.GetSection("Radar:Scheduler"));
        builder.Services.AddSingleton<Formax.Application.Interfaces.ISourceSchedulePlanner,
            Formax.Application.Services.Radar.Sources.Scheduling.SourceSchedulePlanner>();
        builder.Services.AddSingleton<Formax.Application.Interfaces.ISourceExecutionLock,
            Formax.Infrastructure.Radar.Sources.Scheduling.InMemorySourceExecutionLock>();
        builder.Services.AddSingleton<Formax.Application.Interfaces.ISourceScheduleTracker,
            Formax.Infrastructure.Radar.Sources.Scheduling.InMemorySourceScheduleTracker>();

        // ── R.8.3: Radar Collector Layer (dummy collectors, no data collection) ─
        // One Noop collector per configured source type proves the dispatch flow.
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Collection.ISourceCollector>(
            new Formax.Infrastructure.Radar.Sources.Collection.NoopSourceCollector(Formax.Domain.Enums.SourceType.Internal));
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Collection.ISourceCollector>(
            new Formax.Infrastructure.Radar.Sources.Collection.NoopSourceCollector(Formax.Domain.Enums.SourceType.Rss));
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Collection.ISourceCollector>(
            new Formax.Infrastructure.Radar.Sources.Collection.NoopSourceCollector(Formax.Domain.Enums.SourceType.Bridge));
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Collection.ISourceCollectorFactory,
            Formax.Application.Services.Radar.Sources.Collection.SourceCollectorFactory>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Sources.Collection.ISourceCollectorDispatcher,
            Formax.Application.Services.Radar.Sources.Collection.SourceCollectorDispatcher>();

        // ── R.8.4: Radar Normalizer Layer (Collector → Normalizer; no staging) ──
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Normalization.ISourceAliasProvider,
            Formax.Infrastructure.Radar.Sources.Normalization.InMemorySourceAliasProvider>();
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Normalization.ISourceAliasResolver,
            Formax.Application.Services.Radar.Sources.Normalization.SourceAliasResolver>();
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Sources.Normalization.ISourceNormalizer,
            Formax.Application.Services.Radar.Sources.Normalization.NormalizerPipeline>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Sources.Normalization.INormalizerDispatcher,
            Formax.Application.Services.Radar.Sources.Normalization.NormalizerDispatcher>();

        // ── R.8.5: Radar Staging Layer (Collector → Normalizer → Staging) ──────
        builder.Services.AddScoped<Formax.Application.Interfaces.IStagingRepository,
            Formax.Infrastructure.Repositories.StagingRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Sources.Staging.IStagingDispatcher,
            Formax.Application.Services.Radar.Sources.Staging.StagingDispatcher>();

        // ── R.8.6: Radar Health Engine (measurement only; no monitor/alerting) ──
        builder.Services.AddScoped<Formax.Application.Interfaces.ISourceHealthRepository,
            Formax.Infrastructure.Repositories.SourceHealthRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Sources.Health.ISourceHealthService,
            Formax.Application.Services.Radar.Sources.Health.SourceHealthService>();

        // ── R.8.7: Radar Monitor Engine (evaluation only; no notify/failover) ──
        builder.Services.AddScoped<Formax.Application.Interfaces.ISourceMonitorRepository,
            Formax.Infrastructure.Repositories.SourceMonitorRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Sources.Monitor.ISourceMonitorService,
            Formax.Application.Services.Radar.Sources.Monitor.SourceMonitorService>();

        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.RadarSourceScheduler>();

        // ── R.9.1: Radar Match Intelligence Core (consumes match data; no feed/AI) ─
        builder.Services.AddScoped<Formax.Application.Interfaces.IMatchIntelligenceRepository,
            Formax.Infrastructure.Repositories.MatchIntelligenceRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Match.IMatchIntelligenceService,
            Formax.Application.Services.Radar.Intelligence.Match.MatchIntelligenceService>();

        // ── R.9.2: Match Context builder (enriched context; reads DB only) ──────
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Match.IMatchContextBuilder,
            Formax.Application.Services.Radar.Intelligence.Match.MatchContextBuilder>();

        // ── R.9.3: Match Signal Engine (rule-based; consumes MatchContextData) ──
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Match.IMatchSignalEngine,
            Formax.Application.Services.Radar.Intelligence.Match.MatchSignalEngine>();

        // ── R.9.4: Staging consumer — Source Engine → Match Intelligence bridge ─
        builder.Services.AddScoped<Formax.Application.Interfaces.IStagedSourceReader,
            Formax.Infrastructure.Repositories.StagedSourceReader>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Match.IMatchContextEnricher,
            Formax.Application.Services.Radar.Intelligence.Match.MatchContextEnricher>();

        // ── R.9.6: Match Importance Engine (first-class score; rule-based) ──────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Match.IMatchImportanceEngine,
            Formax.Application.Services.Radar.Intelligence.Match.MatchImportanceEngine>();

        // ── R.10.1: News Intelligence Core (deterministic news → match matching) ─
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.News.INewsMatcher,
            Formax.Application.Services.Radar.Intelligence.News.NewsMatcher>();
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.News.INewsClassifier,
            Formax.Application.Services.Radar.Intelligence.News.NewsClassifier>();
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.News.INewsImpactEngine,
            Formax.Application.Services.Radar.Intelligence.News.NewsImpactEngine>();

        // ── R.11.1: Odds Movement Engine (foundation; no collection) ────────────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Odds.IOddsMovementEngine,
            Formax.Application.Services.Radar.Intelligence.Odds.OddsMovementEngine>();

        // ── R.11.2: Odds Movement Orchestrator (time series; no collection) ─────
        builder.Services.AddScoped<Formax.Application.Interfaces.IOddsSnapshotRepository,
            Formax.Infrastructure.Repositories.OddsSnapshotRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Odds.IOddsMovementOrchestrator,
            Formax.Application.Services.Radar.Intelligence.Odds.OddsMovementOrchestrator>();

        // ── R.11.3: Synthetic Odds Engine (internal signals → synthetic movement) ─
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Odds.ISyntheticOddsEngine,
            Formax.Application.Services.Radar.Intelligence.Odds.SyntheticOddsEngine>();

        // ── R.12.1: Commentary Core (deterministic; no AI/LLM) ─────────────────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Commentary.ICommentaryEngine,
            Formax.Application.Services.Radar.Intelligence.Commentary.CommentaryEngine>();
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Intelligence.Commentary.ICommentaryVisibilityEngine,
            Formax.Application.Services.Radar.Intelligence.Commentary.CommentaryVisibilityEngine>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ICommentaryRepository,
            Formax.Infrastructure.Repositories.CommentaryRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.Commentary.ICommentaryService,
            Formax.Application.Services.Radar.Intelligence.Commentary.CommentaryService>();

        // ── R.13.1: Feed Integration Foundation (Radar outputs → feed insights) ─
        builder.Services.AddScoped<Formax.Application.Services.Radar.Feed.IFeedInsightBuilder,
            Formax.Application.Services.Radar.Feed.FeedInsightBuilder>();

        // ── R.13.2: Feed Insight API (query service for /api/radar/feed) ───────
        builder.Services.AddScoped<Formax.Application.Services.Radar.Feed.IFeedInsightQueryService,
            Formax.Application.Services.Radar.Feed.FeedInsightQueryService>();

        // ── R.13.3: Home Feed integration adapter (Radar insight → feed card) ──
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Feed.IRadarFeedAdapter,
            Formax.Application.Services.Radar.Feed.RadarFeedAdapter>();

        // ── R.13.5: Radar ranking support signal (capped ≤15%; config-driven) ──
        builder.Services.Configure<Formax.Application.Services.Radar.Feed.RadarRankingOptions>(
            builder.Configuration.GetSection("RadarRanking"));
        builder.Services.AddScoped<Formax.Application.Services.Radar.Feed.IRadarRankingService,
            Formax.Application.Services.Radar.Feed.RadarRankingService>();

        // ── R.14.1: Learning Event foundation (event collection; no scoring) ───
        builder.Services.AddScoped<Formax.Application.Interfaces.ILearningEventRepository,
            Formax.Infrastructure.Repositories.LearningEventRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Learning.ILearningEventService,
            Formax.Application.Services.Radar.Learning.LearningEventService>();

        // ── R.14.2: User Interest Engine (pure engine + in-memory orchestration) ─
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Learning.IUserInterestEngine,
            Formax.Application.Services.Radar.Learning.UserInterestEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Learning.IUserInterestProfileService,
            Formax.Application.Services.Radar.Learning.UserInterestProfileService>();

        // ── R.14.3: Match Affinity (pure engine + in-memory orchestration) ──────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Learning.IMatchAffinityEngine,
            Formax.Application.Services.Radar.Learning.MatchAffinityEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Learning.IMatchAffinityService,
            Formax.Application.Services.Radar.Learning.MatchAffinityService>();

        // ── R.14.4: League Affinity (pure engine + in-memory orchestration) ─────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Learning.ILeagueAffinityEngine,
            Formax.Application.Services.Radar.Learning.LeagueAffinityEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Learning.ILeagueAffinityService,
            Formax.Application.Services.Radar.Learning.LeagueAffinityService>();

        // ── R.14.5: Radar (Signal) Affinity (pure engine + in-memory) ──────────
        builder.Services.AddSingleton<Formax.Application.Services.Radar.Learning.IRadarAffinityEngine,
            Formax.Application.Services.Radar.Learning.RadarAffinityEngine>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Learning.IRadarAffinityService,
            Formax.Application.Services.Radar.Learning.RadarAffinityService>();
        builder.Services.AddScoped<Formax.Application.Interfaces.INewsIntelligenceRepository,
            Formax.Infrastructure.Repositories.NewsIntelligenceRepository>();
        builder.Services.AddScoped<Formax.Application.Services.Radar.Intelligence.News.INewsIntelligenceService,
            Formax.Application.Services.Radar.Intelligence.News.NewsIntelligenceService>();

        builder.Services.AddSingleton<WorldPerceptionCache>();

        builder.Services.AddScoped<FeedRankingPipeline>();

        builder.Services.AddScoped<UserEmbeddingBuilder>();
        builder.Services.AddScoped<MatchEmbeddingBuilder>();
        builder.Services.AddScoped<AffinityScorer>();

        builder.Services.AddScoped<RankingInputBuilder>();


        builder.Services.AddScoped<IUserTeamFollowRepository, UserTeamFollowRepository>();
        builder.Services.AddScoped<ITeamRepository, TeamRepository>();

        builder.Services.AddScoped<IUserTasteProfileBuilder, UserTasteProfileBuilder>();

        builder.Services.AddScoped<TrendConflictEngine>();

        builder.Services.AddScoped<TasteLearningService>();

        builder.Services.AddScoped<UserProfileEngine>();







        // ---------------- APP ----------------

        var app = builder.Build();

        app.UseCors("Frontend");

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<InfrastructureFailSafeMiddleware>();

        app.MapControllers();


        // ---------------- DB MIGRATION ----------------

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();

            db.Database.Migrate();

            FormaxSeed.Seed(db);

            // Phase 7 validation — multi-user behaviour simulation.
            // Separate + idempotent; depends on FormaxSeed teams/matches.
            TestBehaviorSeed.Seed(db);

            // R.8.1 — sync Radar source registry from appsettings (idempotent).
            var radarRegistryBootstrapper = scope.ServiceProvider
                .GetRequiredService<Formax.Infrastructure.Radar.Sources.RadarSourceRegistryBootstrapper>();
            radarRegistryBootstrapper.SyncAsync().GetAwaiter().GetResult();

            // R.10.1/10.4 — build news intelligence FIRST so match enrichment can read it.
            var newsIntelEarly = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.News.INewsIntelligenceService>();
            var newsMatchesEarly = newsIntelEarly.BuildAsync(DateTime.UtcNow.AddDays(-120)).GetAwaiter().GetResult();
            app.Logger.LogInformation("[NEWS INTEL] built {Count} news match snapshot(s).", newsMatchesEarly);

            // R.9.1 — build match intelligence snapshots (idempotent upsert).
            // R.10.4 — now reads News Intelligence via the enricher.
            var matchIntel = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Match.IMatchIntelligenceService>();
            var intelCount = matchIntel
                .BuildUpcomingAsync(DateTime.UtcNow.AddDays(-120))
                .GetAwaiter().GetResult();
            app.Logger.LogInformation("[MATCH INTEL] built {Count} match intelligence snapshot(s).", intelCount);

            // R.9.2 — verification probe: build enriched context for match id 1.
            var ctxBuilder = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Match.IMatchContextBuilder>();
            var sampleCtx = ctxBuilder.BuildAsync(1).GetAwaiter().GetResult();
            if (sampleCtx is not null)
            {
                // R.9.4 — exercise the staging bridge on the sample context.
                var enricher = scope.ServiceProvider
                    .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Match.IMatchContextEnricher>();
                var enrich = enricher.EnrichAsync(sampleCtx).GetAwaiter().GetResult();

                app.Logger.LogInformation(
                    "[MATCH CONTEXT] match 1: {Home} vs {Away} | homeForm={HForm}({HScore}) awayForm={AForm}({AScore}) | h2h={Meet} meetings avg={Avg} last='{Last}' | importance={Imp} | enrichment(total={Total} news={News} internal={Internal} meta={Meta})",
                    sampleCtx.HomeTeamName, sampleCtx.AwayTeamName,
                    sampleCtx.HomeForm.FormString, sampleCtx.HomeForm.FormScore,
                    sampleCtx.AwayForm.FormString, sampleCtx.AwayForm.FormScore,
                    sampleCtx.H2H.Meetings, sampleCtx.H2H.AvgGoals, sampleCtx.H2H.LastMeetingSummary,
                    sampleCtx.Importance.ImportanceScore,
                    enrich.TotalApplied, enrich.NewsCount, enrich.InternalSignalCount, enrich.MetadataCount);
            }

            // R.11.2 — verification probe: ingest 3 readings (08:00 2.10, 10:00 2.00,
            // 12:00 1.90) for a synthetic test match → must produce 2 movements.
            // Self-cleaning: removes its own test rows first so restarts stay idempotent.
            const int oddsTestMatchId = 999001;
            db.OddsMovementSnapshots.RemoveRange(db.OddsMovementSnapshots.Where(x => x.MatchId == oddsTestMatchId));
            db.OddsSnapshots.RemoveRange(db.OddsSnapshots.Where(x => x.MatchId == oddsTestMatchId));
            db.SaveChanges();

            var oddsOrch = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Odds.IOddsMovementOrchestrator>();
            var baseTime = DateTime.UtcNow.Date.AddHours(8);
            oddsOrch.IngestAsync(new Formax.Domain.Entities.OddsSnapshot { MatchId = oddsTestMatchId, HomeOdds = 2.10, CapturedAtUtc = baseTime }).GetAwaiter().GetResult();
            oddsOrch.IngestAsync(new Formax.Domain.Entities.OddsSnapshot { MatchId = oddsTestMatchId, HomeOdds = 2.00, CapturedAtUtc = baseTime.AddHours(2) }).GetAwaiter().GetResult();
            oddsOrch.IngestAsync(new Formax.Domain.Entities.OddsSnapshot { MatchId = oddsTestMatchId, HomeOdds = 1.90, CapturedAtUtc = baseTime.AddHours(4) }).GetAwaiter().GetResult();

            var oddsRepo = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Interfaces.IOddsSnapshotRepository>();
            var movements = oddsRepo.GetMovementsByMatchAsync(oddsTestMatchId).GetAwaiter().GetResult();
            app.Logger.LogInformation("[ODDS ORCH] test match {Id}: {Count} movement(s)", oddsTestMatchId, movements.Count);
            foreach (var mv in movements)
                app.Logger.LogInformation("[ODDS ORCH]   {Prev} → {Curr} = {Dir}/{Lvl}", mv.PreviousOdds, mv.CurrentOdds, mv.Direction, mv.Level);

            // R.11.3 — synthetic odds signal from internal scores (no real odds).
            var synthEngine = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Odds.ISyntheticOddsEngine>();
            var synthHigh = synthEngine.Evaluate(1, interestScore: 80, newsImpactScore: 100, importanceScore: 100);
            var synthLow = synthEngine.Evaluate(2, interestScore: 5, newsImpactScore: 0, importanceScore: 10);
            app.Logger.LogInformation(
                "[SYNTH ODDS] match {M1}: signal={S1} level={L1} dir={D1} | match {M2}: signal={S2} level={L2} dir={D2}",
                synthHigh.MatchId, synthHigh.SignalScore, synthHigh.Level, synthHigh.Direction,
                synthLow.MatchId, synthLow.SignalScore, synthLow.Level, synthLow.Direction);

            // R.12.1 — build deterministic commentary from intelligence + news.
            var commentary = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Intelligence.Commentary.ICommentaryService>();
            var commentaryCount = commentary.BuildAsync(DateTime.UtcNow.AddDays(-120)).GetAwaiter().GetResult();
            app.Logger.LogInformation("[COMMENTARY] built {Count} commentary snapshot(s).", commentaryCount);

            // R.13.1 — assemble feed insights from Radar outputs (Hidden filtered out).
            var feedBuilder = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Feed.IFeedInsightBuilder>();
            var insights = feedBuilder.BuildAsync(DateTime.UtcNow.AddDays(-120)).GetAwaiter().GetResult();
            var topInsight = insights.OrderByDescending(i => i.ImportanceScore).FirstOrDefault();
            app.Logger.LogInformation(
                "[FEED INSIGHT] {Count} insight(s); top: match={M} importance={Imp} signal={Sig} tone={Tone} vis={Vis} headline='{H}'",
                insights.Count,
                topInsight?.MatchId, topInsight?.ImportanceScore, topInsight?.PrimarySignal,
                topInsight?.CommentaryTone, topInsight?.Visibility, topInsight?.Headline);

            // R.13.3 — adapt the top Radar insight onto feed card shapes.
            var feedQuery = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Feed.IFeedInsightQueryService>();
            var adapter = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Feed.IRadarFeedAdapter>();
            var feedDtos = feedQuery.GetFeedAsync(50).GetAwaiter().GetResult();
            var topDto = feedDtos.FirstOrDefault();
            if (topDto is not null)
            {
                var card = adapter.ToCard(topDto);
                var rec = new Formax.Application.DTOs.Recommendations.RecommendationCardDto { MatchId = topDto.MatchId };
                adapter.Apply(rec, topDto);
                app.Logger.LogInformation(
                    "[RADAR FEED ADAPT] FeedCardModel: match={M} importance={Imp} signal={Sig} headline='{H}' | RecommendationCard: storyHeadline='{SH}' insightLabel='{IL}' (Score unchanged={Sc})",
                    card.MatchId, card.ImportanceScore, card.PrimarySignal, card.Headline,
                    rec.StoryHeadline, rec.InsightLabel, rec.Score);
            }

            // R.13.4 — verify the real recommendation feed assembly overlays Radar content.
            try
            {
                var recUseCase = scope.ServiceProvider.GetRequiredService<GetRecommendationFeedUseCase>();
                // Blended order (what the feed returns).
                var cards = recUseCase.Execute(1, 1, 100).GetAwaiter().GetResult();
                // Pure-RecommendationScore order (what it would be WITHOUT Radar support).
                var recOrder = cards
                    .OrderByDescending(c => c.RecommendationScore).ThenBy(c => c.MatchId)
                    .ToList();
                var overlaid = cards.Where(c => !string.IsNullOrEmpty(c.InsightLabel)).ToList();

                app.Logger.LogInformation(
                    "[RADAR RANKING] {Count} card(s), {Overlaid} radar-overlaid. Position shift (radar cards):",
                    cards.Count, overlaid.Count);

                int moved = 0;
                foreach (var c in overlaid)
                {
                    var blendedPos = cards.FindIndex(x => x.MatchId == c.MatchId) + 1;
                    var recPos = recOrder.FindIndex(x => x.MatchId == c.MatchId) + 1;
                    if (blendedPos < recPos) moved++;
                    app.Logger.LogInformation(
                        "[RADAR RANKING]   match={M} label='{IL}' recScore={RS:F4} | pure-rec pos=#{RP} → radar-blended pos=#{BP} ({Delta})",
                        c.MatchId, c.InsightLabel, c.RecommendationScore, recPos, blendedPos,
                        blendedPos < recPos ? $"+{recPos - blendedPos} up" : blendedPos > recPos ? $"-{blendedPos - recPos} down" : "same");
                }
                app.Logger.LogInformation(
                    "[RADAR RANKING] {Moved}/{Total} radar cards moved UP; rest of feed order preserved.",
                    moved, overlaid.Count);
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "[RADAR FEED ASSEMBLY] probe failed (non-fatal).");
            }

            // R.14.1 — verification probe: a user's Swipe → Detail → Follow journey.
            // Self-cleaning test user so restarts stay idempotent.
            const int learnTestUser = 999001;
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == learnTestUser));
            db.SaveChanges();

            var learning = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Learning.ILearningEventService>();
            learning.RecordSwipeAsync(learnTestUser, 1).GetAwaiter().GetResult();
            learning.RecordDetailOpenAsync(learnTestUser, 1).GetAwaiter().GetResult();
            learning.RecordDetailReturnAsync(learnTestUser, 1, 8200).GetAwaiter().GetResult();
            learning.RecordFollowAsync(learnTestUser, 1).GetAwaiter().GetResult();

            var learnRepo = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Interfaces.ILearningEventRepository>();
            var events = learnRepo.GetByUserAsync(learnTestUser).GetAwaiter().GetResult();
            app.Logger.LogInformation("[LEARNING EVENT] test user {U}: {Count} event(s) recorded.", learnTestUser, events.Count);
            foreach (var e in events.OrderBy(e => e.Id))
                app.Logger.LogInformation("[LEARNING EVENT]   {T} match={M} value={V} source={S}", e.EventType, e.MatchId, e.Value, e.Source);

            // R.14.2 — verification probe: seed a varied journey, compute interest profile.
            const int interestTestUser = 999002;
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == interestTestUser));
            db.SaveChanges();

            // Strong engagement on match 1 (GS-FB, Derby+TitleRace), lighter on 5/7.
            learning.RecordFollowAsync(interestTestUser, 1).GetAwaiter().GetResult();
            learning.RecordDetailReturnAsync(interestTestUser, 1, 9000).GetAwaiter().GetResult();
            learning.RecordDetailOpenAsync(interestTestUser, 5).GetAwaiter().GetResult();
            learning.RecordViewAsync(interestTestUser, 7, 4000).GetAwaiter().GetResult();

            var interestService = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Learning.IUserInterestProfileService>();
            var profile = interestService.GetProfileAsync(interestTestUser).GetAwaiter().GetResult();
            app.Logger.LogInformation(
                "[USER INTEREST] user={U} teams={Teams} leagues={Leagues} signals={Signals}",
                profile.UserId,
                string.Join(", ", profile.Teams.Select(kv => $"{kv.Key}:{kv.Value}")),
                string.Join(", ", profile.Leagues.Select(kv => $"{kv.Key}:{kv.Value}")),
                string.Join(", ", profile.Signals.Select(kv => $"{kv.Key}:{kv.Value}")));

            // R.14.3 — match affinity verification (reuses the seeded interest user).
            // Re-seed (probe above cleaned), compute affinity for a high vs low match.
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == interestTestUser));
            db.SaveChanges();
            // Build a GS-FB / SuperLig / Derby interest from matches that have a populated league.
            learning.RecordFollowAsync(interestTestUser, 6).GetAwaiter().GetResult();        // GS-FB SuperLig Derby
            learning.RecordDetailReturnAsync(interestTestUser, 6, 9000).GetAwaiter().GetResult();
            learning.RecordDetailOpenAsync(interestTestUser, 9).GetAwaiter().GetResult();    // FB-GS SuperLig
            learning.RecordViewAsync(interestTestUser, 7, 4000).GetAwaiter().GetResult();    // GS-BJK SuperLig

            var affinityService = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Learning.IMatchAffinityService>();

            var aHigh = affinityService.GetAffinityAsync(interestTestUser, 6).GetAwaiter().GetResult();   // GS-FB derby (full league)
            // Unrelated match: foreign league/teams the user's profile has no interest in.
            var aLow = affinityService.GetAffinityAsync(interestTestUser, 75).GetAwaiter().GetResult();

            app.Logger.LogInformation(
                "[MATCH AFFINITY] HIGH match={M1} score={S1} level={L1} (team={T1} league={LG1} signal={SG1} imp={I1})",
                aHigh.MatchId, aHigh.AffinityScore, aHigh.AffinityLevel,
                aHigh.TeamComponent, aHigh.LeagueComponent, aHigh.SignalComponent, aHigh.ImportanceComponent);
            app.Logger.LogInformation(
                "[MATCH AFFINITY] LOW  match={M2} score={S2} level={L2} (team={T2} league={LG2} signal={SG2} imp={I2})",
                aLow.MatchId, aLow.AffinityScore, aLow.AffinityLevel,
                aLow.TeamComponent, aLow.LeagueComponent, aLow.SignalComponent, aLow.ImportanceComponent);

            // Self-cleaning.
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == interestTestUser));
            db.SaveChanges();

            // R.14.4 — league affinity verification: multi-league journey.
            const int leagueTestUser = 999003;
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == leagueTestUser));
            db.SaveChanges();
            learning.RecordFollowAsync(leagueTestUser, 6).GetAwaiter().GetResult();        // Süper Lig (heavy)
            learning.RecordDetailReturnAsync(leagueTestUser, 6, 9000).GetAwaiter().GetResult();
            learning.RecordDetailOpenAsync(leagueTestUser, 18).GetAwaiter().GetResult();   // Spanish La Liga 2 (medium)
            learning.RecordViewAsync(leagueTestUser, 21, 3000).GetAwaiter().GetResult();   // Ukrainian First League (light)

            var leagueAffinity = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Learning.ILeagueAffinityService>();
            var leagues = leagueAffinity.GetAsync(leagueTestUser).GetAwaiter().GetResult();
            app.Logger.LogInformation(
                "[LEAGUE AFFINITY] user={U} → {Leagues}",
                leagues.UserId,
                string.Join(", ", leagues.Leagues.Select(l => $"{l.League}:{l.Score}")));

            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == leagueTestUser));
            db.SaveChanges();

            // R.14.5 — radar (signal) affinity verification.
            const int signalTestUser = 999004;
            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == signalTestUser));
            db.SaveChanges();
            learning.RecordFollowAsync(signalTestUser, 6).GetAwaiter().GetResult();        // GS-FB Derby+TitleRace etc.
            learning.RecordDetailReturnAsync(signalTestUser, 6, 9000).GetAwaiter().GetResult();
            learning.RecordDetailOpenAsync(signalTestUser, 9).GetAwaiter().GetResult();
            learning.RecordViewAsync(signalTestUser, 7, 4000).GetAwaiter().GetResult();

            var radarAffinity = scope.ServiceProvider
                .GetRequiredService<Formax.Application.Services.Radar.Learning.IRadarAffinityService>();
            var ra = radarAffinity.GetAsync(signalTestUser).GetAwaiter().GetResult();
            app.Logger.LogInformation(
                "[RADAR AFFINITY] user={U} signals={Signals}",
                ra.UserId, string.Join(", ", ra.Signals.Select(s => $"{s.Signal}:{s.Score}")));
            app.Logger.LogInformation(
                "[RADAR AFFINITY] user={U} groups={Groups}",
                ra.UserId, string.Join(", ", ra.Groups.Select(g => $"{g.Key}:{g.Value}")));

            db.LearningEvents.RemoveRange(db.LearningEvents.Where(x => x.UserId == signalTestUser));
            db.SaveChanges();
        }

        app.Run();
    }
}