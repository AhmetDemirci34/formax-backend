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

        // ── SÜREKLİ ÇALIŞMA (shadow deployment) ────────────────────────────────────
        // Bind adresi yapılandırılabilir; VARSAYILAN DEĞİŞMEDİ. Gölge modda çalışan bir
        // sunucuda uygun değer yine "http://localhost:5063"tür: dışarıya hiçbir şey açılmaz,
        // gölge iş kendi içinde koşar, sağlık ucu yalnız makinenin kendisinden okunur.
        builder.WebHost.UseUrls(builder.Configuration["Hosting:Urls"] ?? "http://localhost:5063");

        // Windows Service olarak kurulduğunda SCM ile konuşur (Start/Stop/Recovery).
        // Konsoldan `dotnet run` ile çalıştırıldığında bu çağrı NO-OP'tur — geliştirme
        // davranışı birebir aynı kalır.
        builder.Host.UseWindowsService();

        // Kalıcı log: konsol süreçle ölür, sunucuda konsol yoktur. Varsayılan KAPALI.
        var fileLog = new Formax.Infrastructure.Logging.FileLoggerOptions
        {
            Enabled = builder.Configuration.GetValue<bool>("Logging:File:Enabled"),
            Directory = builder.Configuration["Logging:File:Directory"] ?? "Logs",
            FilePrefix = builder.Configuration["Logging:File:FilePrefix"] ?? "formax",
            RetainedDays = builder.Configuration.GetValue<int?>("Logging:File:RetainedDays") ?? 30
        };
        if (fileLog.Enabled)
        {
            builder.Logging.AddProvider(
                new Formax.Infrastructure.Logging.FileLoggerProvider(fileLog, builder.Environment.ContentRootPath));
        }

        // 🔥 SADECE BUNU EKLEDİM
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

                // ZAMAN SÖZLEŞMESİ — tüm DateTime alanları "…Z" (UTC) yazılır.
                // Depodan (EF/SQL Server) gelen DateTime'ın Kind'ı Unspecified olduğu için
                // eki olmayan "2026-08-18T19:00:00" üretiliyordu; tarayıcı bunu YEREL saat
                // sayıyor ve TR kullanıcısında kickoff 3 saat geriye kayıyordu (ölçüldü:
                // Fenerbahçe–Lyon 19:00Z → UI 19:00 → 19:06'da "başlamış" görünüyordu).
                // Değer değişmez; yalnız UTC olduğu bilgisi eklenir. Bkz. UtcDateTimeJsonConverter.
                options.JsonSerializerOptions.Converters.Add(new Formax.API.Serialization.UtcDateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new Formax.API.Serialization.NullableUtcDateTimeJsonConverter());
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

        // ---------------- GDP: provider bootstrap (onaylı ücretsiz provider'lar + config seed) ----------------
        Formax.Infrastructure.Providers.Bootstrap.ProviderBootstrapServiceCollectionExtensions
            .AddProviderBootstrap(builder.Services);

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
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchSocialPostsUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchAiSignalsUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchDecisionUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchVoiceUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchNarrativeUseCase>();

        builder.Services.AddScoped<IOynanmaSinyalProvider, OynanmaSinyalProvider_Default>();

        builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        builder.Services.AddScoped<IMatchBanditRepository, MatchBanditRepository>();

        builder.Services.AddScoped<GetUserNotificationsUseCase>();

        builder.Services.AddScoped<IFaiOverviewService, FaiOverviewService>();

        // AIAnalysisService (sabit 0.5 stub) MVP audit'te kaldırıldı — frontend kullanmıyordu;
        // gerçek analiz MarketProbabilityEngine → GET /api/matches/{id}/decision.

        builder.Services.AddScoped<WorldExpectationService>();






        builder.Services.AddScoped<IMatchReadRepository, MatchReadRepository>();

        // ── SEZON KAPSAMI + İÇ KAYNAKLI PUAN DURUMU (30.08.2026) ──────────────
        // "Bu sezon" tanımı tek yerden çözülür; puan durumu kendi tamamlanmış
        // maçlarımızdan saatlik projeksiyonla üretilir (dış istek YOK).
        builder.Services.AddScoped<Formax.Application.Interfaces.ILeagueSeasonResolver,
                                   Formax.Infrastructure.Seasons.LeagueSeasonResolver>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ILeagueStandingsService,
                                   Formax.Infrastructure.Standings.LeagueStandingsService>();
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

        // "SENİN SEÇİMİN" — olası sonuç seçimleri. Mevcut UserPicks tablosu genişletildi;
        // paralel ikinci bir tahmin sistemi KURULMADI.
        builder.Services.AddScoped<Formax.Application.UseCases.Picks.UserPickSelectionUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.Picks.GetUserPredictionsUseCase>();




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

        // InterestDecayService DI'da kayıtlı DEĞİLDİ ve hiçbir yerden çağrılmıyordu → decay hiç
        // çalışmıyordu. UserTrendService bunu read-time'da tükettiği için burada kaydedilir
        // (yeni scheduler/HostedService KURULMADAN minimum entegrasyon).
        builder.Services.AddScoped<Formax.Application.Services.Recommendation.InterestDecayService>();

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

        // Discovery Engine — orkestrasyon katmanı (Recommendation üreticisini kullanır).
        builder.Services.AddScoped<Formax.Application.Interfaces.Discovery.IDiscoveryEngine,
            Formax.Application.Services.Discovery.DiscoveryEngine>();

        builder.Services.AddScoped<GetHomeRadarUseCase>();

        builder.Services.AddScoped<GetLiveMatchReadingUseCase>();

        builder.Services.AddScoped<EmitMatchEventUseCase>();

        builder.Services.AddScoped<GetLiveMatchReadingUseCase>();
        builder.Services.AddScoped<EmitMatchEventUseCase>();

        // ── MatchLiveController DI zinciri — kayıtsızdı, /api/live/{id} 500 veriyordu ──
        // YALNIZCA eksik + MEVCUT tipler register edilir (yeni servis/interface/impl/factory YOK).
        builder.Services.AddScoped<GetLiveMatchTimelineUseCase>();
        builder.Services.AddScoped<GetLiveMatchScreenUseCase>();
        builder.Services.AddScoped<GetLiveMatchAiAnalysisUseCase>();
        builder.Services.AddScoped<UserExperienceContextFactory>();
        builder.Services.AddScoped<UserExperienceUpdater>();
        builder.Services.AddScoped<AiSpeakDecisionInputBuilder>();
        builder.Services.AddScoped<AiSpeakDecisionService>();
        builder.Services.AddScoped<AiSpeakWindow>();
        builder.Services.AddScoped<ContextInsufficientEvaluator>();
        builder.Services.AddScoped<FatigueEvaluator>();
        builder.Services.AddScoped<RepetitionEvaluator>();
        builder.Services.AddScoped<StatePermissionEvaluator>();
        builder.Services.AddScoped<IAiSpeakTelemetryRepository, AiSpeakTelemetryRepository>();
        builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        builder.Services.AddScoped<IIntroAccessRepository, IntroAccessRepository>();

        builder.Services.AddScoped<MatchEventService>();

        builder.Services.AddScoped<GetUsersFollowingMatchUseCase>();
        builder.Services.AddScoped<GetFollowedMatchesUseCase>();
        // "Takip Ettiğim Maçlar" ekranı — yalnız maç takipleri, gerçek durum, iki bölüm.
        builder.Services.AddScoped<Formax.Application.UseCases.Follow.GetFollowedMatchesScreenUseCase>();
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
        // POST /api/users/me/teams kayıtsızdı → 500. Takım takibi bildirimlerin de
        // girdisi olduğu için bu, takım-takibi bildirim zincirini tamamen kapatıyordu.
        builder.Services.AddScoped<SetMyTeamsUseCase>();

        // ── Lig takibi (team follow mimarisi referans) + takip özeti + toplu okundu ──
        builder.Services.AddScoped<Formax.Application.UseCases.Leagues.GetMyLeaguesUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.Leagues.FollowLeagueUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.Leagues.UnfollowLeagueUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.Follow.GetFollowSummaryUseCase>();


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
        // Tek kimlik otoritesine geçiş — bir defalık/idempotent re-key bakım servisi.
        builder.Services.AddScoped<Formax.Infrastructure.Maintenance.CanonicalIdentityRekeyService>();
        builder.Services.AddScoped<Formax.Application.Services.Fixtures.FixtureDiscoveryService>();
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

        // ── SON DAKİKA: liste üretimi + dil çevirisi ──────────────────────────────
        // Liste TEK yerde üretilir (/detail ve /news aynı servisi kullanır).
        builder.Services.AddScoped<Formax.Application.Services.News.Feed.MatchNewsFeedService>();
        // Çeviri kalıcı önbelleği — (ContentHash, Language) ile; aynı haber iki kez çevrilmez.
        builder.Services.AddScoped<Formax.Application.Interfaces.IMatchNewsTranslationRepository,
            Formax.Infrastructure.Repositories.MatchNewsTranslationRepository>();
        // Çeviri MEVCUT ILLMClient zinciriyle yapılır — yeni çeviri servisi eklenmedi.
        builder.Services.AddScoped<Formax.Application.Services.News.Translation.NewsTranslationService>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchNewsUseCase>();
        // ── CANLI TAKİP + ÖNEMLİ ANLAR ────────────────────────────────────────────
        // İkisi de AI Maç Analizi zincirinden BAĞIMSIZDIR; mevcut haber/sosyal/olay
        // depolarını okur, yeni sağlayıcı veya toplama işi eklemez.
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchLiveFeedUseCase>();
        builder.Services.AddScoped<Formax.Application.UseCases.GetMatchHighlightsUseCase>();

        // ── AI ANLATI ISITMA (PREWARM) ────────────────────────────────────────────
        // Yaklasan maclarin anlatisini ARKA PLANDA uretir; kullanicinin /detail istegi
        // hazir snapshot'a duser. AI Mac Analizi'nin kendisi (uc/prompt/model/guard/UI)
        // DEGISMEZ - yalniz uretim zamani one alinir. Kapatma: Narrative:PrewarmEnabled=false.
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.NarrativePrewarmJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.NarrativePrewarmJob>());
        builder.Services.AddHttpClient<Formax.Application.Services.News.Discovery.INewsProvider,
            Formax.Infrastructure.News.Providers.GoogleNewsRssProvider>();
        builder.Services.AddHttpClient<Formax.Application.Services.News.Discovery.INewsProvider,
            Formax.Infrastructure.News.Providers.BingNewsRssProvider>();
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.NewsDiscoveryJob>();

        // MAÇ SONRASI VİDEO — bitmiş maçların RESMÎ videosunu ÖNCEDEN DB'ye yazar;
        // böylece maç özeti tıklaması 0 dış istek üretir. Canlı polling yoktur ve
        // api-football kotasına dokunulmaz.
        // MAÇ SONRASI OLAY + İSTATİSTİK — aynı turun ilk aşaması. Ayrı bir tekrar eden
        // iş DEĞİLDİR: kendi kalıcı defteri ve günlük tavanı vardır, video aşamasından
        // bağımsız çalışır. Kullanıcının maç sayfasını açması bu servisi TETİKLEMEZ.
        builder.Services.AddScoped<Formax.Infrastructure.PostMatch.PostMatchDataIngestionService>();

        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.PostMatchEnrichmentJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.PostMatchEnrichmentJob>());

        // ---------------- Phase 7 — SOCIAL DISCOVERY (resmi sosyal medya) ----------------
        // Platform-genişletilebilir ISocialProvider koleksiyonu (yeni platform = yeni satır).
        // YouTube RSS gerçek+key'siz; X/IG/FB kimlik-bilgisi olmadan IsEnabled=false (fake yok).
        builder.Services.AddSingleton<Formax.Application.Services.Social.Discovery.ISocialProvider,
            Formax.Infrastructure.Social.Providers.YouTubeRssSocialProvider>();
        // Singleton + hosted → admin manuel tetik (AdminSocialController) için de çözülür.
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.SocialDiscoveryJob>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.SocialDiscoveryJob>());

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
            Formax.Infrastructure.Providers.ApiFootballPlayerStatsProvider>()
            .ConfigureHttpClient(c => c.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            // Bu istemci daha önce METERING'siz kaydedilmişti → istekleri telemetride GÖRÜNMÜYORDU.
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballCacheHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballMeteringHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballResilienceHandler>();
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
        // FORMAX AI Evolution — GDP-türevli sinyalleri motora tek context olarak veren builder.
        builder.Services.AddScoped<Formax.Application.AI.Context.IMatchAiContextBuilder, Formax.Application.AI.Context.MatchAiContextBuilder>();
        // FAZ 1 — TeamComparison/H2H'ın TEK kaynağı (Detail use-case + AI context builder aynı sınıfı kullanır).
        // Scoped: istek-içi memoizasyon (Discover tek istekte yüzlerce aday değerlendirir).
        builder.Services.AddScoped<Formax.Application.Services.Matches.MatchComparisonFactory>();

        // ---------------- WORLD JOBS ----------------

        builder.Services.AddScoped<WorldPerceptionProvider>();

        // Register once as singleton, then add the hosted service from the same
        // instance — lets an admin endpoint resolve the job to trigger
        // RefreshStandingsAsync manually (test) without a second instance.
        builder.Services.AddSingleton<WorldPerceptionDailyJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorldPerceptionDailyJob>());
        builder.Services.AddHostedService<SapmaSnapshotJob>();

        // SAATLİK PUAN DURUMU PROJEKSİYONU — kendi sonuçlarımızdan; dış istek üretmez.
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.HourlyStandingsProjectionJob>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.HourlyStandingsProjectionJob>());

        // ── Sprint 0: Fixture sync ─────────────────────────────────────────────
        // Singleton + hosted: admin geri-doldurma ucu (POST /admin/fixtures/backfill)
        // AYNI örneği çözüp geçmiş pencere için sync tetikleyebilsin diye (Odds/Lineup deseni).
        builder.Services.AddSingleton<FixtureSyncJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<FixtureSyncJob>());

        // Historical backfill (Sprint 19B) — singleton + hosted so an admin
        // endpoint can trigger RunCycleAsync manually for testing.
        builder.Services.AddSingleton<HistoricalSyncJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<HistoricalSyncJob>());

        // Football Intelligence v1.0 — player/squad ingestion (12h; coverage varsa doldurur).
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.PlayerIntelligenceSyncJob>();

        // ── Prediction Contract V1 — SHADOW MODE ──────────────────────────────────
        // Kilitli motor (INDEPENDENT_POISSON_V2 / TEAM_STRENGTH_V2 / GATE_V1 / CalibrationVersion=NONE)
        // yaklaşan gerçek maçlar için tahmin üretir ve YALNIZ DB'ye yazar. Kullanıcıya hiçbir şey
        // gösterilmez; hiçbir uç bu tabloları okumaz. İstek yolunda yeri yoktur.
        // Varsayılan KAPALI — Predictions:ShadowMode:Enabled=true ile açılır.
        // EngineRoot BOŞ ise (Production profili böyle verir) ContentRoot'un bir üstü kullanılır.
        // Boş dizgiyi "verilmiş" saymak, sunucuda motor dosyalarını yanlış yerde aratıp job'ı
        // sessizce devre dışı bırakırdı; bu yüzden boşluk da "verilmemiş" sayılır.
        var configuredEngineRoot = builder.Configuration["Predictions:EngineRoot"];
        builder.Services.AddSingleton(new Formax.Infrastructure.Predictions.PredictionEngineOptions
        {
            EngineRoot = string.IsNullOrWhiteSpace(configuredEngineRoot)
                         ? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."))
                         : configuredEngineRoot,
            StartupDelaySeconds = builder.Configuration.GetValue<int?>("Predictions:StartupDelaySeconds") ?? 120,
            LoopHours = builder.Configuration.GetValue<double?>("Predictions:LoopHours") ?? 6,
            HorizonDays = builder.Configuration.GetValue<int?>("Predictions:HorizonDays") ?? 8
        });
        builder.Services.AddScoped<Formax.Infrastructure.Predictions.ShadowPredictionService>();
        builder.Services.AddScoped<Formax.Infrastructure.Predictions.PredictionSettlementService>();

        // Sağlık durumu HER ZAMAN kayıtlıdır (gölge kapalıyken de): /admin/shadow/health o
        // durumda "NOT_STARTED" der. Ölçüm yüzeyinin varlığı gölgenin açık olmasına bağlı olmamalı.
        builder.Services.AddSingleton<Formax.Infrastructure.Predictions.ShadowHealthState>();

        if (builder.Configuration.GetValue<bool>("Predictions:ShadowMode:Enabled"))
        {
            builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.ShadowPredictionJob>();
            builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.PredictionSettlementJob>();
        }

        // ── SHADOW B — NEWS_ADJUSTED deney hattı ──────────────────────────────────
        // Shadow A'nın YANINDA çalışır, yerine değil. A'nın yazdığı satırları yalnız OKUR;
        // kendi tablolarına (ShadowBPredictions / …Evidence / …Settlements) yazar. Model,
        // λ formülleri, TeamStrength, Poisson, Gate ve Prediction Contract V1 DEĞİŞMEZ.
        // Yüzdeyi backend matematiği belirler — bu yolda LLM yoktur.
        // Varsayılan KAPALI — Predictions:ShadowB:Enabled=true ile açılır.
        builder.Services.AddSingleton(new Formax.Infrastructure.Predictions.NewsAdjustOptions
        {
            K = builder.Configuration.GetValue<double?>("Predictions:ShadowB:K") ?? 0.35,
            MaxTilt = builder.Configuration.GetValue<double?>("Predictions:ShadowB:MaxTilt") ?? 0.40,
            MaxImpactPerSide = builder.Configuration.GetValue<double?>("Predictions:ShadowB:MaxImpactPerSide") ?? 1.0,
            MinSourceQuality = builder.Configuration.GetValue<int?>("Predictions:ShadowB:MinSourceQuality") ?? 85,
            MinConfidence = builder.Configuration.GetValue<int?>("Predictions:ShadowB:MinConfidence") ?? 60
        });
        builder.Services.AddSingleton(new Formax.Infrastructure.BackgroundJobs.ShadowBOptions
        {
            StartupDelaySeconds = builder.Configuration.GetValue<int?>("Predictions:ShadowB:StartupDelaySeconds") ?? 180,
            LoopHours = builder.Configuration.GetValue<double?>("Predictions:ShadowB:LoopHours") ?? 3,
            HorizonDays = builder.Configuration.GetValue<int?>("Predictions:ShadowB:HorizonDays") ?? 8
        });
        builder.Services.AddScoped<Formax.Infrastructure.Predictions.ShadowBPredictionService>();

        if (builder.Configuration.GetValue<bool>("Predictions:ShadowB:Enabled"))
        {
            builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.ShadowBPredictionJob>();
        }

        // GDP scheduler — mevcut IGlobalDataPipeline'ı maç başına çağırır (yeni pipeline YOK).
        // Singleton + hosted: HistoricalSyncJob/OddsIngestionJob ile aynı desen, böylece bir admin
        // ucu aynı örneği enjekte edip RunOnceAsync tetikleyebilir. Varsayılan KAPALI (Gdp:Enabled=false).
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.GdpSyncJob>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.GdpSyncJob>());

        // ── Memory Cache (IMemoryCache — used by ApiFootballSportsDataProvider) ──
        builder.Services.AddMemoryCache();

        // ── MVP Release Hardening: api-football HTTP dayanıklılık handler'ı ──────
        // Retry + exponential backoff + 429/5xx + per-attempt timeout + errors[]/boş
        // response introspection. Yeni paket YOK (DelegatingHandler). Aşağıdaki 3
        // ApiFootball HttpClient'ına eklenir; timeout Infinite → süreyi handler bounded tutar.
        builder.Services.AddTransient<Formax.Infrastructure.Http.ApiFootballResilienceHandler>();

        // ── Timeline Operations — GERÇEK API istek ölçümü (metering) + senkron telemetrisi ──
        builder.Services.AddSingleton<Formax.Infrastructure.Telemetry.ApiFootballMetrics>();
        // İstek başına SIRSIZ kayıt (job, uç, fikstür, cache, bütçe, HTTP sonucu). Video keşfinin
        // sayacı API-Football'dan tamamen AYRIDIR.
        builder.Services.AddSingleton<Formax.Infrastructure.Telemetry.ApiFootballRequestLog>();
        builder.Services.AddSingleton<Formax.Infrastructure.Telemetry.VideoDiscoveryRequestLog>();
        builder.Services.AddSingleton<Formax.Infrastructure.Telemetry.TimelineSyncTelemetry>();
        builder.Services.AddTransient<Formax.Infrastructure.Http.ApiFootballMeteringHandler>();
        // Job attribution — pipeline'ın EN İÇİNE eklenir (resilience'tan SONRA), böylece her
        // gerçek HTTP denemesi (retry dahil) job × endpoint olarak sayılır. Metering EN DIŞTA
        // kalır → mevcut RequestsByEndpoint sayacının anlamı DEĞİŞMEZ.
        builder.Services.AddTransient<Formax.Infrastructure.Http.ApiFootballJobAttributionHandler>();

        // ── FORMAX VERİ KATMANI — api-footballa giden TEK kapı (L1 memory → L2 kalıcı → HTTP).
        // EN DIŞ handlerdır: cache isabetinde alt katmanlar hiç çalışmaz, gerçek istek doğmaz.
        // Ayrıca single-flight (aynı anahtar için eşzamanlı N çağrı = 1 HTTP) ve günlük bütçe.
        builder.Services.AddSingleton<Formax.Infrastructure.Http.ApiFootballHttpCacheStore>();
        builder.Services.AddTransient<Formax.Infrastructure.Http.ApiFootballCacheHandler>();

        // ── Sprint 20A+20B: API-Football H2H enrichment ──────────────────────
        builder.Services.AddHttpClient("ApiFootball")
            .ConfigureHttpClient(c => c.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballCacheHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballMeteringHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballResilienceHandler>();
        builder.Services.AddSingleton<Formax.Infrastructure.Cache.H2HCache>();
        builder.Services.AddScoped<Formax.Application.Interfaces.IH2HProvider,
            Formax.Infrastructure.Providers.ApiFootballH2HProvider>();

        // ── Sprint 1: Lineup engine ──────────────────────────────────────────
        // Active sports data provider — api-football (v3) is FORMAX'ın TEK futbol veri
        // sağlayıcısı. Fixtures/lineup/live stats/live events/momentum/standings/competition
        // context tümü buradan gelir; DTO'lar ve job'lar değişmedi. Cache-aside korunur.
        builder.Services.AddHttpClient<ISportsDataProvider, ApiFootballSportsDataProvider>()
            .ConfigureHttpClient(c => c.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballCacheHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballMeteringHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballResilienceHandler>()
            .AddHttpMessageHandler<Formax.Infrastructure.Http.ApiFootballJobAttributionHandler>();
        builder.Services.AddScoped<IMatchLineupRepository, MatchLineupRepository>();
        builder.Services.AddScoped<IMatchPlayerStatusRepository, MatchPlayerStatusRepository>();
        // KADRO: API-Football kadro servisi (LineupIngestionService) artık DI'da YOK — kadro
        // yalnız resmî kaynaktan (OfficialLineupCollector, Infrastructure DI) toplanır.
        // Singleton + hosted: admin teşhis ucu (POST /admin/lineup/sync) AYNI örneği
        // çözüp tek maç için ingestion tetikleyebilsin diye (Odds ile aynı desen).
        // RESMÎ MAÇ MERKEZİ — kritik gelişme + resmî başlama saati (admin tetiği aynı örnek).
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.OfficialMatchCentreJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.OfficialMatchCentreJob>());
        // AI MAÇ ANALİZİ — yaklaşan maçlar için arka planda kanıttan üretim (admin tetiği aynı örnek).
        builder.Services.AddSingleton<Formax.Infrastructure.BackgroundJobs.MatchAnalysisJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Formax.Infrastructure.BackgroundJobs.MatchAnalysisJob>());
        builder.Services.AddSingleton<LineupIngestionJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<LineupIngestionJob>());

        // ── Real Market Odds: sağlayıcı → MatchMarketOdds → Decision/Feed ─────
        // Job hem hosted service hem admin tetiklemesi (POST /admin/odds/sync) için kayıtlı.
        builder.Services.AddScoped<Formax.Application.Interfaces.IMatchOddsRepository,
            Formax.Infrastructure.Repositories.MatchOddsRepository>();
        builder.Services.AddSingleton<OddsIngestionJob>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<OddsIngestionJob>());

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

        // ── MVP Freeze / Cold Start: News+Match Intelligence ve Commentary build'leri ──
        // Eskiden app.Run() ÖNCESİNDE senkron çalışıyorlardı (13.6k maç → 5 dk+ açılış).
        // Aynı çağrılar aynı sırayla buraya taşındı; artık arka planda çalışırlar.
        builder.Services.AddHostedService<Formax.Infrastructure.BackgroundJobs.RadarIntelligenceBuildJob>();

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
        builder.Services.AddScoped<IUserLeagueFollowRepository, UserLeagueFollowRepository>();
        builder.Services.AddScoped<ITeamRepository, TeamRepository>();

        builder.Services.AddScoped<IUserTasteProfileBuilder, UserTasteProfileBuilder>();

        builder.Services.AddScoped<TrendConflictEngine>();

        builder.Services.AddScoped<TasteLearningService>();

        builder.Services.AddScoped<UserProfileEngine>();







        // ── MVP FREEZE — EKSİK DI KAYITLARI (endpoint doğrulamasında 500 verenler) ────
        // Aşağıdaki controller'lar çözülemeyen bağımlılık yüzünden 500 dönüyordu. YALNIZCA
        // ZATEN VAR OLAN tipler kaydedilir — yeni servis/interface/implementasyon YOK.
        //
        //  /api/prediction-types              → PredictionTypeService kayıtsızdı
        //  /api/admin/ai/metrics*             → controller SOMUT AiStateMetricsReadRepository
        //                                        istiyor; yalnız interface kayıtlıydı
        //  /api/admin/ai/state-transitions    → StateTransitionLogReadRepository kayıtsızdı
        //  /api/admin/ai-metrics              → GetAiSpeakMetricsUseCase zinciri kayıtsızdı
        //  /api/coupons/*                     → Coupons özelliğinin TAMAMI (repo+use case)
        //                                        kayıtsızdı; controller hiç kurulamıyordu
        builder.Services.AddScoped<PredictionTypeService>();

        // /admin/users/{userId} + premium aç/kapa — kayıtsızdı (bağımlılıkları IUserRepository, kayıtlı).
        builder.Services.AddScoped<GetAdminUserDetailUseCase>();
        builder.Services.AddScoped<EnableUserPremiumUseCase>();
        builder.Services.AddScoped<DisableUserPremiumUseCase>();

        builder.Services.AddScoped<Formax.Infrastructure.Repositories.AiStateMetricsReadRepository>();
        builder.Services.AddScoped<Formax.Infrastructure.Repositories.StateTransitionLogReadRepository>();

        builder.Services.AddScoped<Formax.Application.Interfaces.IAiSpeakTelemetryReadRepository,
            Formax.Infrastructure.Repositories.AiSpeakTelemetryReadRepository>();
        builder.Services.AddScoped<Formax.Application.Services.AdminDashboard.AiSpeakMetricsCalculator>();
        builder.Services.AddScoped<Formax.Application.UseCases.Admin.GetAiSpeakMetricsUseCase>();

        builder.Services.AddScoped<Formax.Application.Interfaces.ICouponReadRepository,
            Formax.Infrastructure.Repositories.CouponReadRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ICouponWriteRepository,
            Formax.Infrastructure.Repositories.CouponWriteRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ICouponItemReadRepository,
            Formax.Infrastructure.Repositories.CouponItemReadRepository>();
        builder.Services.AddScoped<Formax.Application.Interfaces.ICouponItemWriteRepository,
            Formax.Infrastructure.Repositories.CouponItemWriteRepository>();
        builder.Services.AddScoped<AICouponCommentService>();
        builder.Services.AddScoped<CreateCouponUseCase>();
        builder.Services.AddScoped<AddCouponItemUseCase>();
        builder.Services.AddScoped<EvaluateCouponUseCase>();
        builder.Services.AddScoped<GetCouponsByResultUseCase>();
        builder.Services.AddScoped<GetCouponDetailUseCase>();

        // ---------------- APP ----------------

        var app = builder.Build();

        // ---------------- GDP: provider başlangıç kayıtları (BaseUrl'ler ProviderBootstrap katmanından) ----------------
        using (var gdpScope = app.Services.CreateScope())
        {
            gdpScope.ServiceProvider
                .GetRequiredService<Formax.Infrastructure.Providers.Bootstrap.ProviderBootstrap>()
                .Run();
        }

        // LLM ÇAĞRI KAPSAMI — kullanıcı isteği sırasında yapılan her LLM çağrısı
        // "request:{yol}" olarak sayılır (sayısal kimlikler {id}'ye indirgenir). Kullanıcı
        // sayfasının LLM çağırmadığı bu sayaçla kanıtlanır: GET /admin/analysis/report.
        app.Use(async (ctx, next) =>
        {
            var path = System.Text.RegularExpressions.Regex.Replace(ctx.Request.Path.Value ?? "/", @"/\d+", "/{id}");
            using (Formax.Application.AI.LLM.LlmCallMeter.Begin("request:" + path))
                await next();
        });

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

        // ── MVP FREEZE — COLD START ──────────────────────────────────────────────
        // app.Run() öncesinde YALNIZCA API'nin ayağa kalkması için zorunlu, sınırlı
        // maliyetli işler kalır: migration, boş-DB tohumu ve kaynak kaydı senkronu.
        //
        // Buradan ÇIKARILANLAR:
        //  • TestBehaviorSeed  → production'a test kullanıcısı/aksiyonu yazıyordu (test data).
        //  • Odds test maçı 999001, learning test kullanıcıları 999001-999004 → test data.
        //  • MatchContext / SyntheticOdds / FeedInsight / RadarFeedAdapt / RadarRanking
        //    doğrulama probe'ları → yalnız log üretiyorlardı; RadarRanking probe'u ayrıca
        //    her açılışta TAM öneri feed'ini kuruyordu.
        //  • News + Match Intelligence + Commentary build'leri → gerçek veri üretirler,
        //    bu yüzden SİLİNMEDİ; RadarIntelligenceBuildJob'a taşındılar (aynı sıra,
        //    aynı 120 günlük pencere), artık app.Run() SONRASINDA arka planda çalışırlar.
        //
        // Sebep: bu blok 120 günlük pencerede ~13.6k maçı senkron işliyordu ve API
        // 5 dakikadan uzun süre hiçbir isteği kabul etmiyordu.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();

            db.Database.Migrate();

            // Boş veritabanı bootstrap'ı — dolu DB'de guard'lar sayesinde no-op.
            FormaxSeed.Seed(db);

            // R.8.1 — sync Radar source registry from appsettings (idempotent, hafif).
            var radarRegistryBootstrapper = scope.ServiceProvider
                .GetRequiredService<Formax.Infrastructure.Radar.Sources.RadarSourceRegistryBootstrapper>();
            radarRegistryBootstrapper.SyncAsync().GetAwaiter().GetResult();
        }

        app.Run();
    }
}
