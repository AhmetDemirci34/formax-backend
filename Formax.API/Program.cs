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





var builder = WebApplication.CreateBuilder(args);

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
    options.AddPolicy("AllowFrontend",
        p => p.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
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

builder.Services.AddScoped<IOynanmaSinyalProvider, OynanmaSinyalProvider_Default>();




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








// ---------------- WORLD JOBS ----------------

builder.Services.AddScoped<WorldPerceptionProvider>();

builder.Services.AddHostedService<WorldPerceptionDailyJob>();
builder.Services.AddHostedService<SapmaSnapshotJob>();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseMiddleware<InfrastructureFailSafeMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


// ---------------- DB MIGRATION ----------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();

    db.Database.Migrate();

    FormaxSeed.Seed(db);
}

app.Run();