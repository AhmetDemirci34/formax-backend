IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [AIAnalyses] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NULL,
        [CouponId] int NULL,
        [Probability] float NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastExtendedContextKey] int NOT NULL,
        CONSTRAINT [PK_AIAnalyses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [AIDecisionTraces] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [MatchId] int NOT NULL,
        [ConfidenceScore] float NOT NULL,
        [GuardrailDecision] nvarchar(max) NOT NULL,
        [AiBehaviorState] nvarchar(max) NOT NULL,
        [MemoryDecayApplied] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIDecisionTraces] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [AISelfInvalidationLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [MatchId] int NOT NULL,
        [InitialConfidence] float NOT NULL,
        [EffectiveConfidence] float NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AISelfInvalidationLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [AIWeightConfigs] (
        [Id] int NOT NULL IDENTITY,
        [InterestWeight] float NOT NULL,
        [BanditWeight] float NOT NULL,
        [SessionWeight] float NOT NULL,
        [TrendWeight] float NOT NULL,
        [DiversityWeight] float NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIWeightConfigs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [AiSpeakTelemetries] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NULL,
        [MatchId] int NOT NULL,
        [CanSpeak] bit NOT NULL,
        [SilenceReason] nvarchar(max) NULL,
        [AccessLevel] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AiSpeakTelemetries] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Result] nvarchar(max) NULL,
        [AIComment] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [FeedInteractionEvents] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [UserId] int NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [SignalType] int NOT NULL,
        [DwellTimeSeconds] float NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Reward] float NOT NULL,
        [ScoreAtServe] float NULL,
        [PositionAtServe] int NULL,
        [DecisionType] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_FeedInteractionEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [FeedScoreLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [MatchId] int NOT NULL,
        [InterestScore] float NOT NULL,
        [BanditScore] float NOT NULL,
        [ExplorationScore] float NOT NULL,
        [AffinityScore] float NOT NULL,
        [SessionScore] float NOT NULL,
        [TrendScore] float NOT NULL,
        [NarrativeScore] float NOT NULL,
        [DominanceBoost] float NOT NULL,
        [PersonalBoost] float NOT NULL,
        [SkipPenalty] float NOT NULL,
        [FinalScore] float NOT NULL,
        [IsExplore] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RankPosition] int NOT NULL,
        [DecisionType] nvarchar(max) NOT NULL,
        [WeightSnapshot] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_FeedScoreLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [IntroAccesses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [MaxMatchCount] int NOT NULL,
        [ConsumedMatchCount] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        CONSTRAINT [PK_IntroAccesses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [LastExtendedContextKeys] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [ContextKey] int NOT NULL,
        [ExtendedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LastExtendedContextKeys] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchDiscoveryNodes] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [X] float NOT NULL,
        [Y] float NOT NULL,
        [Intensity] float NOT NULL,
        [Cluster] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchDiscoveryNodes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [Matches] (
        [Id] int NOT NULL IDENTITY,
        [HomeTeamId] int NOT NULL,
        [AwayTeamId] int NOT NULL,
        [MatchDate] datetime2 NOT NULL,
        [HomeScore] int NOT NULL,
        [AwayScore] int NOT NULL,
        [MatchMinute] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [League] nvarchar(120) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastEmittedEventType] nvarchar(max) NULL,
        [LeagueId] int NOT NULL,
        CONSTRAINT [PK_Matches] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchEvents] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Minute] int NOT NULL,
        [TeamName] nvarchar(max) NULL,
        [PlayerName] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchExplorationStats] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [ExplorationImpressions] int NOT NULL,
        [ExplorationClicks] int NOT NULL,
        [ExplorationScore] float NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchExplorationStats] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchFeedStates] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [State] nvarchar(max) NOT NULL,
        [Impressions] int NOT NULL,
        [Clicks] int NOT NULL,
        [Score] float NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchFeedStates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchLiveEvents] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Minute] int NOT NULL,
        [Team] nvarchar(max) NULL,
        [Player] nvarchar(max) NULL,
        [ImpactScore] float NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchLiveEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchNarratives] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Story] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [NarrativeScore] float NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchNarratives] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchOynanmaSnapshots] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [OynanmaSkoru] int NOT NULL,
        [CapturedAtUtc] datetime2 NOT NULL,
        [Source] nvarchar(max) NULL,
        CONSTRAINT [PK_MatchOynanmaSnapshots] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchRecommendationStats] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [Impressions] int NOT NULL,
        [Clicks] int NOT NULL,
        [Dwells] int NOT NULL,
        [Skips] int NOT NULL,
        [Follows] int NOT NULL,
        [RewardScore] float NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchRecommendationStats] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchRewardStats] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [Impressions] int NOT NULL,
        [ClickCount] int NOT NULL,
        [OpenCount] int NOT NULL,
        [FollowCount] int NOT NULL,
        [SkipCount] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchRewardStats] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchSapmaSnapshots] (
        [MatchId] int NOT NULL,
        [Sapma] int NOT NULL,
        [Bolge] nvarchar(50) NOT NULL,
        [SessizMi] bit NOT NULL,
        [ComputedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchSapmaSnapshots] PRIMARY KEY ([MatchId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [MatchTrendStats] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [Impressions] int NOT NULL,
        [Clicks] int NOT NULL,
        [Dwells] int NOT NULL,
        [Goals] int NOT NULL,
        [TrendScore] float NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchTrendStats] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [PredictionTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [GroupCode] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PredictionTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [SessionInteractions] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [SessionId] nvarchar(max) NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionInteractions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [StateTransitionLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [MatchId] int NOT NULL,
        [FromState] int NOT NULL,
        [ToState] int NOT NULL,
        [Trigger] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StateTransitionLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [Period] int NOT NULL,
        [StartDateUtc] datetime2 NOT NULL,
        [EndDateUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [LeagueRank] int NOT NULL,
        [AvgGoalsFor] float NOT NULL,
        [AvgGoalsAgainst] float NOT NULL,
        [IsStableTeam] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserContextMemories] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] int NOT NULL,
        [LastLeague] nvarchar(max) NULL,
        [LastTeam] nvarchar(max) NULL,
        [LastContentType] nvarchar(max) NULL,
        [RecentClicks] int NOT NULL,
        [RecentDwells] int NOT NULL,
        [LastInteractionAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserContextMemories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserInterestEvents] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [EventType] int NOT NULL,
        [MatchId] int NULL,
        [TeamId] int NULL,
        [LeagueName] nvarchar(150) NULL,
        [Weight] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserInterestEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserInterestScoreEntity] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Layer] nvarchar(450) NOT NULL,
        [Key] nvarchar(450) NOT NULL,
        [Score] int NOT NULL,
        [LastEventAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserInterestScoreEntity] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserInterestScores] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Layer] nvarchar(max) NOT NULL,
        [Key] nvarchar(max) NOT NULL,
        [Score] int NOT NULL,
        [LastEventAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserInterestScores] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserMatchFollows] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [MatchId] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsLiveTrackingEnabled] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserMatchFollows] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserNotifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [MatchId] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(max) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [MatchesWithAiCount] int NOT NULL,
        [AiContextShownCount] int NOT NULL,
        [LastAiInteractionAt] datetime2 NULL,
        [LastAiInteractionType] nvarchar(max) NULL,
        [HasSeenRegisterHint] bit NOT NULL,
        [IsPremium] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [LastExtendedContextKey] nvarchar(max) NULL,
        [FirstSessionCompleted] bit NOT NULL,
        [TotalInteractions] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserSessionInterests] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] int NOT NULL,
        [Key] nvarchar(max) NOT NULL,
        [League] nvarchar(max) NULL,
        [Team] nvarchar(max) NULL,
        [Clicks] int NOT NULL,
        [Dwells] int NOT NULL,
        [LastInteractionAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserSessionInterests] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserTeamFollows] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TeamId] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserTeamFollows] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [UserWeightProfiles] (
        [UserId] int NOT NULL,
        [AffinityWeight] float NOT NULL,
        [BanditWeight] float NOT NULL,
        [ExplorationWeight] float NOT NULL,
        [InterestWeight] float NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserWeightProfiles] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE TABLE [CouponItems] (
        [Id] int NOT NULL IDENTITY,
        [CouponId] int NOT NULL,
        [MatchId] int NOT NULL,
        [PredictionTypeId] int NOT NULL,
        [IsSuccess] bit NULL,
        CONSTRAINT [PK_CouponItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponItems_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponItems_PredictionTypes_PredictionTypeId] FOREIGN KEY ([PredictionTypeId]) REFERENCES [PredictionTypes] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_CouponItems_CouponId] ON [CouponItems] ([CouponId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_CouponItems_PredictionTypeId] ON [CouponItems] ([PredictionTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_MatchSapmaSnapshots_ExpiresAtUtc] ON [MatchSapmaSnapshots] ([ExpiresAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_UserInterestEvents_UserId_CreatedAtUtc] ON [UserInterestEvents] ([UserId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_UserInterestEvents_UserId_EventType] ON [UserInterestEvents] ([UserId], [EventType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserInterestScoreEntity_UserId_Layer_Key] ON [UserInterestScoreEntity] ([UserId], [Layer], [Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    CREATE INDEX [IX_UserTeamFollows_UserId_TeamId] ON [UserTeamFollows] ([UserId], [TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260327141531_InitClean'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260327141531_InitClean', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328202800_Add_LastReward_Field'
)
BEGIN
    ALTER TABLE [MatchRecommendationStats] ADD [LastReward] float NOT NULL DEFAULT 0.0E0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328202800_Add_LastReward_Field'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260328202800_Add_LastReward_Field', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328204421_Add_RewardScore_To_FeedScoreLog'
)
BEGIN
    ALTER TABLE [FeedScoreLogs] ADD [RewardScore] float NOT NULL DEFAULT 0.0E0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328204421_Add_RewardScore_To_FeedScoreLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260328204421_Add_RewardScore_To_FeedScoreLog', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    ALTER TABLE [Teams] ADD [ColorPrimary] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    ALTER TABLE [Teams] ADD [ColorSecondary] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    ALTER TABLE [Teams] ADD [LogoUrl] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    CREATE INDEX [IX_Matches_AwayTeamId] ON [Matches] ([AwayTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    CREATE INDEX [IX_Matches_HomeTeamId] ON [Matches] ([HomeTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    ALTER TABLE [Matches] ADD CONSTRAINT [FK_Matches_Teams_AwayTeamId] FOREIGN KEY ([AwayTeamId]) REFERENCES [Teams] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    ALTER TABLE [Matches] ADD CONSTRAINT [FK_Matches_Teams_HomeTeamId] FOREIGN KEY ([HomeTeamId]) REFERENCES [Teams] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406092647_AddTeamMediaFields_Fixed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406092647_AddTeamMediaFields_Fixed', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    ALTER TABLE [CouponItems] DROP CONSTRAINT [FK_CouponItems_PredictionTypes_PredictionTypeId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DROP TABLE [UserInterestScoreEntity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DROP INDEX [IX_UserTeamFollows_UserId_TeamId] ON [UserTeamFollows];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DROP INDEX [IX_UserInterestEvents_UserId_CreatedAtUtc] ON [UserInterestEvents];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DROP INDEX [IX_UserInterestEvents_UserId_EventType] ON [UserInterestEvents];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DROP INDEX [IX_MatchSapmaSnapshots_ExpiresAtUtc] ON [MatchSapmaSnapshots];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserInterestEvents]') AND [c].[name] = N'LeagueName');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserInterestEvents] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [UserInterestEvents] ALTER COLUMN [LeagueName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MatchSapmaSnapshots]') AND [c].[name] = N'Bolge');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [MatchSapmaSnapshots] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [MatchSapmaSnapshots] ALTER COLUMN [Bolge] nvarchar(max) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    CREATE TABLE [UserConfidence] (
        [UserId] nvarchar(450) NOT NULL,
        [Value] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId1] int NOT NULL,
        CONSTRAINT [PK_UserConfidence] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserConfidence_Users_UserId1] FOREIGN KEY ([UserId1]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    CREATE TABLE [UserPicks] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(max) NOT NULL,
        [MatchId] int NOT NULL,
        [PickLabel] nvarchar(max) NOT NULL,
        [Confidence] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPicks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    CREATE TABLE [UserPickStats] (
        [UserId] nvarchar(450) NOT NULL,
        [PickLabel] nvarchar(450) NOT NULL,
        [Total] int NOT NULL,
        [Win] int NOT NULL,
        CONSTRAINT [PK_UserPickStats] PRIMARY KEY ([UserId], [PickLabel])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    CREATE TABLE [UserStats] (
        [UserId] nvarchar(450) NOT NULL,
        [Total] int NOT NULL,
        [Win] int NOT NULL,
        [Lose] int NOT NULL,
        [CurrentStreak] int NOT NULL,
        [BestStreak] int NOT NULL,
        [Level] int NOT NULL,
        CONSTRAINT [PK_UserStats] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    CREATE INDEX [IX_UserConfidence_UserId1] ON [UserConfidence] ([UserId1]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    ALTER TABLE [CouponItems] ADD CONSTRAINT [FK_CouponItems_PredictionTypes_PredictionTypeId] FOREIGN KEY ([PredictionTypeId]) REFERENCES [PredictionTypes] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407082931_Fix_All_User_PK'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407082931_Fix_All_User_PK', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    ALTER TABLE [UserConfidence] DROP CONSTRAINT [FK_UserConfidence_Users_UserId1];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    DROP INDEX [IX_UserConfidence_UserId1] ON [UserConfidence];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserConfidence]') AND [c].[name] = N'UserId1');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [UserConfidence] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [UserConfidence] DROP COLUMN [UserId1];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    ALTER TABLE [UserStats] ADD [PassCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    ALTER TABLE [UserStats] ADD [PlayCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    ALTER TABLE [UserStats] ADD [RiskPreference] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    ALTER TABLE [UserStats] ADD [SafePreference] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409123339_AddUserStatsFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409123339_AddUserStatsFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409190732_Unique_UserPick'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserPicks]') AND [c].[name] = N'UserId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [UserPicks] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [UserPicks] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409190732_Unique_UserPick'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPicks_UserId_MatchId] ON [UserPicks] ([UserId], [MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409190732_Unique_UserPick'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409190732_Unique_UserPick', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412202827_AddMatchTrendFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260412202827_AddMatchTrendFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414081603_AddUserTasteProfile'
)
BEGIN
    CREATE TABLE [UserTasteProfiles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RiskLevel] float NOT NULL,
        [TrendAffinity] float NOT NULL,
        [ValueSeeking] float NOT NULL,
        [TotalSwipes] int NOT NULL,
        CONSTRAINT [PK_UserTasteProfiles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414081603_AddUserTasteProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414081603_AddUserTasteProfile', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teams]') AND [c].[name] = N'LeagueRank');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Teams] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Teams] ALTER COLUMN [LeagueRank] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teams]') AND [c].[name] = N'IsStableTeam');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Teams] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Teams] ALTER COLUMN [IsStableTeam] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teams]') AND [c].[name] = N'AvgGoalsFor');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Teams] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Teams] ALTER COLUMN [AvgGoalsFor] float NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teams]') AND [c].[name] = N'AvgGoalsAgainst');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Teams] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Teams] ALTER COLUMN [AvgGoalsAgainst] float NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    CREATE TABLE [UserActions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [MatchId] int NOT NULL,
        [ActionType] nvarchar(max) NOT NULL,
        [Odds] float NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserActions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416090010_AddUserActions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416090010_AddUserActions', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417165605_Fix_UserId_Type'
)
BEGIN
    ALTER TABLE [UserStats] DROP CONSTRAINT [PK_UserStats];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417165605_Fix_UserId_Type'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserStats]') AND [c].[name] = N'UserId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [UserStats] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [UserStats] ALTER COLUMN [UserId] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417165605_Fix_UserId_Type'
)
BEGIN
    ALTER TABLE [UserStats] ADD [ViewCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417165605_Fix_UserId_Type'
)
BEGIN
    ALTER TABLE [UserStats] ADD CONSTRAINT [PK_UserStats] PRIMARY KEY ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417165605_Fix_UserId_Type'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260417165605_Fix_UserId_Type', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423173038_AddUserBehaviorFields'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserActions]') AND [c].[name] = N'ActionType');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [UserActions] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [UserActions] ALTER COLUMN [ActionType] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423173038_AddUserBehaviorFields'
)
BEGIN
    ALTER TABLE [UserActions] ADD [Followed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423173038_AddUserBehaviorFields'
)
BEGIN
    ALTER TABLE [UserActions] ADD [OpenedDetail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423173038_AddUserBehaviorFields'
)
BEGIN
    ALTER TABLE [UserActions] ADD [ViewDurationMs] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423173038_AddUserBehaviorFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423173038_AddUserBehaviorFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426075235_AddUserBehaviorFields2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426075235_AddUserBehaviorFields2', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502183112_AddLearningSystem'
)
BEGIN
    CREATE TABLE [GlobalTrends] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [LikeRate] float NOT NULL,
        [SkipRate] float NOT NULL,
        [Score] float NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        CONSTRAINT [PK_GlobalTrends] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502183112_AddLearningSystem'
)
BEGIN
    CREATE TABLE [MatchBanditStats] (
        [MatchId] int NOT NULL,
        [Impressions] int NOT NULL,
        [Likes] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchBanditStats] PRIMARY KEY ([MatchId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502183112_AddLearningSystem'
)
BEGIN
    CREATE TABLE [UserPreferenceWeights] (
        [UserId] int NOT NULL,
        [LikeWeight] float NOT NULL,
        [SkipWeight] float NOT NULL,
        [TeamWeight] float NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPreferenceWeights] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502183112_AddLearningSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260502183112_AddLearningSystem', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    ALTER TABLE [Matches] ADD [ExternalMatchId] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    CREATE TABLE [MatchLineupPlayers] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [Side] nvarchar(10) NOT NULL,
        [Role] nvarchar(10) NOT NULL,
        [ShirtNumber] int NOT NULL,
        [PlayerName] nvarchar(120) NOT NULL,
        [Position] nvarchar(5) NOT NULL,
        [IsCaptain] bit NOT NULL,
        CONSTRAINT [PK_MatchLineupPlayers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    CREATE TABLE [MatchLineups] (
        [MatchId] int NOT NULL,
        [HomeLineupsReleased] bit NOT NULL,
        [AwayLineupsReleased] bit NOT NULL,
        [ReleasedAt] datetime2 NULL,
        [FetchedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchLineups] PRIMARY KEY ([MatchId]),
        CONSTRAINT [FK_MatchLineups_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    CREATE TABLE [MatchPlayerStatuses] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [TeamId] int NOT NULL,
        [PlayerName] nvarchar(120) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Reason] nvarchar(256) NOT NULL,
        [FetchedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchPlayerStatuses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    CREATE INDEX [IX_MatchLineupPlayers_MatchId] ON [MatchLineupPlayers] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    CREATE INDEX [IX_MatchPlayerStatuses_MatchId] ON [MatchPlayerStatuses] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523162814_Sprint1_LineupEngine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523162814_Sprint1_LineupEngine', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523163841_Sprint2_StandingsAndContext'
)
BEGIN
    CREATE TABLE [CompetitionContexts] (
        [MatchId] int NOT NULL,
        [CompetitionType] nvarchar(20) NOT NULL,
        [StageName] nvarchar(120) NOT NULL,
        [ContextHeadline] nvarchar(200) NOT NULL,
        [ContextSummary] nvarchar(500) NOT NULL,
        [BracketJson] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CompetitionContexts] PRIMARY KEY ([MatchId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523163841_Sprint2_StandingsAndContext'
)
BEGIN
    CREATE TABLE [LeagueExternalMappings] (
        [LeagueId] int NOT NULL,
        [ExternalLeagueId] nvarchar(50) NOT NULL,
        [SeasonYear] int NOT NULL,
        [LeagueName] nvarchar(120) NOT NULL,
        CONSTRAINT [PK_LeagueExternalMappings] PRIMARY KEY ([LeagueId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523163841_Sprint2_StandingsAndContext'
)
BEGIN
    CREATE TABLE [LeagueStandings] (
        [LeagueId] int NOT NULL,
        [SeasonYear] int NOT NULL,
        [TeamId] int NOT NULL,
        [TeamName] nvarchar(120) NOT NULL,
        [Position] int NOT NULL,
        [Played] int NOT NULL,
        [Won] int NOT NULL,
        [Drawn] int NOT NULL,
        [Lost] int NOT NULL,
        [GoalsFor] int NOT NULL,
        [GoalsAgainst] int NOT NULL,
        [Points] int NOT NULL,
        [Form] nvarchar(20) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LeagueStandings] PRIMARY KEY ([LeagueId], [SeasonYear], [TeamId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523163841_Sprint2_StandingsAndContext'
)
BEGIN
    CREATE INDEX [IX_LeagueStandings_LeagueId_SeasonYear] ON [LeagueStandings] ([LeagueId], [SeasonYear]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523163841_Sprint2_StandingsAndContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523163841_Sprint2_StandingsAndContext', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523171204_Sprint3_LiveMatchIntelligence'
)
BEGIN
    ALTER TABLE [MatchLiveEvents] ADD [Detail] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523171204_Sprint3_LiveMatchIntelligence'
)
BEGIN
    CREATE TABLE [MatchLiveStats] (
        [MatchId] int NOT NULL,
        [HomeScore] int NOT NULL,
        [AwayScore] int NOT NULL,
        [Minute] int NULL,
        [Phase] nvarchar(10) NOT NULL,
        [PossessionHome] int NOT NULL,
        [PossessionAway] int NOT NULL,
        [ShotsHome] int NOT NULL,
        [ShotsAway] int NOT NULL,
        [ShotsOnTargetHome] int NOT NULL,
        [ShotsOnTargetAway] int NOT NULL,
        [CornersHome] int NOT NULL,
        [CornersAway] int NOT NULL,
        [FoulsHome] int NOT NULL,
        [FoulsAway] int NOT NULL,
        [OffsidesHome] int NOT NULL,
        [OffsidesAway] int NOT NULL,
        [YellowHome] int NOT NULL,
        [YellowAway] int NOT NULL,
        [RedHome] int NOT NULL,
        [RedAway] int NOT NULL,
        [DangerousAttacksHome] int NOT NULL,
        [DangerousAttacksAway] int NOT NULL,
        [XgHome] float NULL,
        [XgAway] float NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchLiveStats] PRIMARY KEY ([MatchId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523171204_Sprint3_LiveMatchIntelligence'
)
BEGIN
    CREATE TABLE [MatchMomentumSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] int NOT NULL,
        [MinuteBucket] int NOT NULL,
        [HomePressure] int NOT NULL,
        [AwayPressure] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchMomentumSnapshots] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523171204_Sprint3_LiveMatchIntelligence'
)
BEGIN
    CREATE INDEX [IX_MatchMomentumSnapshots_MatchId_MinuteBucket] ON [MatchMomentumSnapshots] ([MatchId], [MinuteBucket]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523171204_Sprint3_LiveMatchIntelligence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523171204_Sprint3_LiveMatchIntelligence', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523173234_Sprint3b_DistributedLock'
)
BEGIN
    CREATE TABLE [LiveIngestionLocks] (
        [Id] int NOT NULL,
        [OwnerInstanceId] nvarchar(200) NOT NULL,
        [AcquiredAt] datetime2 NOT NULL,
        [HeartbeatAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LiveIngestionLocks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523173234_Sprint3b_DistributedLock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523173234_Sprint3b_DistributedLock', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523180757_Sprint4_NabizFeedIntelligence'
)
BEGIN
    CREATE TABLE [MatchSocialFeedItems] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NULL,
        [TeamId] int NULL,
        [Source] nvarchar(200) NOT NULL,
        [SourceType] int NOT NULL,
        [Author] nvarchar(200) NOT NULL,
        [AuthorVerified] bit NOT NULL,
        [Headline] nvarchar(500) NOT NULL,
        [Summary] nvarchar(600) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [SourceUrl] nvarchar(1000) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [SentimentScore] float NULL,
        [RelevanceScore] float NOT NULL,
        [ContentHash] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MatchSocialFeedItems] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523180757_Sprint4_NabizFeedIntelligence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchSocialFeedItems_ContentHash] ON [MatchSocialFeedItems] ([ContentHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523180757_Sprint4_NabizFeedIntelligence'
)
BEGIN
    CREATE INDEX [IX_MatchSocialFeedItems_MatchId_PublishedAt] ON [MatchSocialFeedItems] ([MatchId], [PublishedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523180757_Sprint4_NabizFeedIntelligence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523180757_Sprint4_NabizFeedIntelligence', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523183550_Sprint0_TeamExternalId'
)
BEGIN
    ALTER TABLE [Teams] ADD [ExternalTeamId] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523183550_Sprint0_TeamExternalId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523183550_Sprint0_TeamExternalId', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524061450_Sprint0_FixtureSync_Indices'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teams]') AND [c].[name] = N'ExternalTeamId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Teams] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Teams] ALTER COLUMN [ExternalTeamId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524061450_Sprint0_FixtureSync_Indices'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Matches]') AND [c].[name] = N'ExternalMatchId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Matches] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [Matches] ALTER COLUMN [ExternalMatchId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524061450_Sprint0_FixtureSync_Indices'
)
BEGIN
    CREATE INDEX [IX_Teams_ExternalTeamId] ON [Teams] ([ExternalTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524061450_Sprint0_FixtureSync_Indices'
)
BEGIN
    CREATE INDEX [IX_Matches_ExternalMatchId] ON [Matches] ([ExternalMatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524061450_Sprint0_FixtureSync_Indices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524061450_Sprint0_FixtureSync_Indices', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    DROP INDEX [IX_Teams_ExternalTeamId] ON [Teams];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    DROP INDEX [IX_Matches_ExternalMatchId] ON [Matches];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    CREATE TABLE [FixtureSyncLocks] (
        [Id] int NOT NULL,
        [OwnerInstanceId] nvarchar(200) NOT NULL,
        [AcquiredAt] datetime2 NOT NULL,
        [HeartbeatAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FixtureSyncLocks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Teams_ExternalTeamId] ON [Teams] ([ExternalTeamId]) WHERE [ExternalTeamId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Matches_ExternalMatchId] ON [Matches] ([ExternalMatchId]) WHERE [ExternalMatchId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524062659_Sprint0_FixtureSyncCriticalFixes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524062659_Sprint0_FixtureSyncCriticalFixes', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524065025_Sprint0_Match_RefereeVenue'
)
BEGIN
    ALTER TABLE [Matches] ADD [Referee] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524065025_Sprint0_Match_RefereeVenue'
)
BEGIN
    ALTER TABLE [Matches] ADD [Venue] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524065025_Sprint0_Match_RefereeVenue'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524065025_Sprint0_Match_RefereeVenue', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525000000_AddUserActionTeam'
)
BEGIN
    ALTER TABLE [UserActions] ADD [Team] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525000000_AddUserActionTeam'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525000000_AddUserActionTeam', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602000000_AddApiFootballTeamId'
)
BEGIN
    ALTER TABLE [Teams] ADD [ApiFootballTeamId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260602000000_AddApiFootballTeamId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260602000000_AddApiFootballTeamId', N'8.0.8');
END;
GO

COMMIT;
GO

