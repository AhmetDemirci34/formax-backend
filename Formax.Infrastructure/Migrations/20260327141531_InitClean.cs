using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    CouponId = table.Column<int>(type: "int", nullable: true),
                    Probability = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastExtendedContextKey = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIDecisionTraces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    GuardrailDecision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiBehaviorState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemoryDecayApplied = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIDecisionTraces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AISelfInvalidationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    InitialConfidence = table.Column<double>(type: "float", nullable: false),
                    EffectiveConfidence = table.Column<double>(type: "float", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AISelfInvalidationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIWeightConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterestWeight = table.Column<double>(type: "float", nullable: false),
                    BanditWeight = table.Column<double>(type: "float", nullable: false),
                    SessionWeight = table.Column<double>(type: "float", nullable: false),
                    TrendWeight = table.Column<double>(type: "float", nullable: false),
                    DiversityWeight = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIWeightConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiSpeakTelemetries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    CanSpeak = table.Column<bool>(type: "bit", nullable: false),
                    SilenceReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSpeakTelemetries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AIComment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedInteractionEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignalType = table.Column<int>(type: "int", nullable: false),
                    DwellTimeSeconds = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reward = table.Column<double>(type: "float", nullable: false),
                    ScoreAtServe = table.Column<double>(type: "float", nullable: true),
                    PositionAtServe = table.Column<int>(type: "int", nullable: true),
                    DecisionType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedInteractionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedScoreLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    InterestScore = table.Column<double>(type: "float", nullable: false),
                    BanditScore = table.Column<double>(type: "float", nullable: false),
                    ExplorationScore = table.Column<double>(type: "float", nullable: false),
                    AffinityScore = table.Column<double>(type: "float", nullable: false),
                    SessionScore = table.Column<double>(type: "float", nullable: false),
                    TrendScore = table.Column<double>(type: "float", nullable: false),
                    NarrativeScore = table.Column<double>(type: "float", nullable: false),
                    DominanceBoost = table.Column<double>(type: "float", nullable: false),
                    PersonalBoost = table.Column<double>(type: "float", nullable: false),
                    SkipPenalty = table.Column<double>(type: "float", nullable: false),
                    FinalScore = table.Column<double>(type: "float", nullable: false),
                    IsExplore = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RankPosition = table.Column<int>(type: "int", nullable: false),
                    DecisionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WeightSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedScoreLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntroAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxMatchCount = table.Column<int>(type: "int", nullable: false),
                    ConsumedMatchCount = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntroAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LastExtendedContextKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ContextKey = table.Column<int>(type: "int", nullable: false),
                    ExtendedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastExtendedContextKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchDiscoveryNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    Intensity = table.Column<double>(type: "float", nullable: false),
                    Cluster = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchDiscoveryNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeScore = table.Column<int>(type: "int", nullable: false),
                    AwayScore = table.Column<int>(type: "int", nullable: false),
                    MatchMinute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    League = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEmittedEventType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchExplorationStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExplorationImpressions = table.Column<int>(type: "int", nullable: false),
                    ExplorationClicks = table.Column<int>(type: "int", nullable: false),
                    ExplorationScore = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchExplorationStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchFeedStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchFeedStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchLiveEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    Team = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Player = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImpactScore = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLiveEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchNarratives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Story = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NarrativeScore = table.Column<double>(type: "float", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchNarratives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchOynanmaSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    OynanmaSkoru = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchOynanmaSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchRecommendationStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    Dwells = table.Column<int>(type: "int", nullable: false),
                    Skips = table.Column<int>(type: "int", nullable: false),
                    Follows = table.Column<int>(type: "int", nullable: false),
                    RewardScore = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRecommendationStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchRewardStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    ClickCount = table.Column<int>(type: "int", nullable: false),
                    OpenCount = table.Column<int>(type: "int", nullable: false),
                    FollowCount = table.Column<int>(type: "int", nullable: false),
                    SkipCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRewardStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchSapmaSnapshots",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Sapma = table.Column<int>(type: "int", nullable: false),
                    Bolge = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SessizMi = table.Column<bool>(type: "bit", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSapmaSnapshots", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "MatchTrendStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    Dwells = table.Column<int>(type: "int", nullable: false),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    TrendScore = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTrendStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PredictionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StateTransitionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    FromState = table.Column<int>(type: "int", nullable: false),
                    ToState = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateTransitionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeagueRank = table.Column<int>(type: "int", nullable: false),
                    AvgGoalsFor = table.Column<double>(type: "float", nullable: false),
                    AvgGoalsAgainst = table.Column<double>(type: "float", nullable: false),
                    IsStableTeam = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserContextMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LastLeague = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastTeam = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecentClicks = table.Column<int>(type: "int", nullable: false),
                    RecentDwells = table.Column<int>(type: "int", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContextMemories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInterestEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    LeagueName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterestEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInterestScoreEntity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterestScoreEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInterestScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterestScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMatchFollows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLiveTrackingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMatchFollows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchesWithAiCount = table.Column<int>(type: "int", nullable: false),
                    AiContextShownCount = table.Column<int>(type: "int", nullable: false),
                    LastAiInteractionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAiInteractionType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasSeenRegisterHint = table.Column<bool>(type: "bit", nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastExtendedContextKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstSessionCompleted = table.Column<bool>(type: "bit", nullable: false),
                    TotalInteractions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessionInterests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    League = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Team = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    Dwells = table.Column<int>(type: "int", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessionInterests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTeamFollows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTeamFollows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWeightProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AffinityWeight = table.Column<double>(type: "float", nullable: false),
                    BanditWeight = table.Column<double>(type: "float", nullable: false),
                    ExplorationWeight = table.Column<double>(type: "float", nullable: false),
                    InterestWeight = table.Column<double>(type: "float", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWeightProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "CouponItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CouponId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PredictionTypeId = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CouponItems_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CouponItems_PredictionTypes_PredictionTypeId",
                        column: x => x.PredictionTypeId,
                        principalTable: "PredictionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CouponItems_CouponId",
                table: "CouponItems",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_CouponItems_PredictionTypeId",
                table: "CouponItems",
                column: "PredictionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSapmaSnapshots_ExpiresAtUtc",
                table: "MatchSapmaSnapshots",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestEvents_UserId_CreatedAtUtc",
                table: "UserInterestEvents",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestEvents_UserId_EventType",
                table: "UserInterestEvents",
                columns: new[] { "UserId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInterestScoreEntity_UserId_Layer_Key",
                table: "UserInterestScoreEntity",
                columns: new[] { "UserId", "Layer", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTeamFollows_UserId_TeamId",
                table: "UserTeamFollows",
                columns: new[] { "UserId", "TeamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIAnalyses");

            migrationBuilder.DropTable(
                name: "AIDecisionTraces");

            migrationBuilder.DropTable(
                name: "AISelfInvalidationLogs");

            migrationBuilder.DropTable(
                name: "AIWeightConfigs");

            migrationBuilder.DropTable(
                name: "AiSpeakTelemetries");

            migrationBuilder.DropTable(
                name: "CouponItems");

            migrationBuilder.DropTable(
                name: "FeedInteractionEvents");

            migrationBuilder.DropTable(
                name: "FeedScoreLogs");

            migrationBuilder.DropTable(
                name: "IntroAccesses");

            migrationBuilder.DropTable(
                name: "LastExtendedContextKeys");

            migrationBuilder.DropTable(
                name: "MatchDiscoveryNodes");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "MatchExplorationStats");

            migrationBuilder.DropTable(
                name: "MatchFeedStates");

            migrationBuilder.DropTable(
                name: "MatchLiveEvents");

            migrationBuilder.DropTable(
                name: "MatchNarratives");

            migrationBuilder.DropTable(
                name: "MatchOynanmaSnapshots");

            migrationBuilder.DropTable(
                name: "MatchRecommendationStats");

            migrationBuilder.DropTable(
                name: "MatchRewardStats");

            migrationBuilder.DropTable(
                name: "MatchSapmaSnapshots");

            migrationBuilder.DropTable(
                name: "MatchTrendStats");

            migrationBuilder.DropTable(
                name: "SessionInteractions");

            migrationBuilder.DropTable(
                name: "StateTransitionLogs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "UserContextMemories");

            migrationBuilder.DropTable(
                name: "UserInterestEvents");

            migrationBuilder.DropTable(
                name: "UserInterestScoreEntity");

            migrationBuilder.DropTable(
                name: "UserInterestScores");

            migrationBuilder.DropTable(
                name: "UserMatchFollows");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "UserSessionInterests");

            migrationBuilder.DropTable(
                name: "UserTeamFollows");

            migrationBuilder.DropTable(
                name: "UserWeightProfiles");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "PredictionTypes");
        }
    }
}
