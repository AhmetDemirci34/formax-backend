using Microsoft.EntityFrameworkCore;
using Formax.Domain.Entities;
using Formax.Domain.States;
using Formax.Domain.Subscriptions;
using Formax.Infrastructure.Data.Entities;
using Formax.Application.Abstractions;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.Data
{
    public class FormaxDbContext : DbContext, IAppDbContext
    {
        public FormaxDbContext(DbContextOptions<FormaxDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserStats> UserStats { get; set; } = null!;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);

        public DbSet<UserConfidence> UserConfidence { get; set; }
        public DbSet<UserPickStats> UserPickStats { get; set; }
        public DbSet<UserPick> UserPicks { get; set; }

        public DbSet<Team> Teams { get; set; }
        public DbSet<Match> Matches { get; set; }

        public DbSet<MatchOynanmaSnapshot> MatchOynanmaSnapshots { get; set; }
        public DbSet<MatchSapmaSnapshot> MatchSapmaSnapshots { get; set; }

        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponItem> CouponItems { get; set; }

        public DbSet<PredictionType> PredictionTypes { get; set; }
        public DbSet<AIAnalysis> AIAnalyses { get; set; }

        public DbSet<UserMatchFollow> UserMatchFollows { get; set; }
        public DbSet<UserTeamFollow> UserTeamFollows { get; set; }
        public DbSet<UserLeagueFollow> UserLeagueFollows { get; set; }

        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; } = null!;

        // ── Resmî kaynak altyapısı (kadro/sonuç/olay/istatistik/kritik gelişme) ──
        public DbSet<OfficialSourceFetch> OfficialSourceFetches { get; set; } = null!;
        public DbSet<OfficialSourceCacheEntry> OfficialSourceCache { get; set; } = null!;
        public DbSet<OfficialMatchLink> OfficialMatchLinks { get; set; } = null!;

        /// <summary>Kanonik takım ↔ sağlayıcı takım kimliği (resmî UEFA fikstür kaynağı).</summary>
        public DbSet<TeamProviderIdentity> TeamProviderIdentities { get; set; } = null!;
        public DbSet<MatchAnalysisSnapshot> MatchAnalysisSnapshots { get; set; } = null!;
        public DbSet<MatchCriticalDevelopment> MatchCriticalDevelopments { get; set; } = null!;
        public DbSet<MatchPostMatchSummary> MatchPostMatchSummaries { get; set; } = null!;
        public DbSet<OfficialVideoSourceRecord> OfficialVideoSourceCatalog { get; set; } = null!;
        public DbSet<MatchVideoDiscoveryQueueItem> MatchVideoDiscoveryQueue { get; set; } = null!;
        public DbSet<MatchVideoDiscoveryAttempt> MatchVideoDiscoveryAttempts { get; set; } = null!;
        public DbSet<VideoDiscoveryCursor> VideoDiscoveryCursors { get; set; } = null!;
        public DbSet<OfficialWebFeed> OfficialWebFeeds { get; set; } = null!;
        public DbSet<OfficialWebVideoEntry> OfficialWebVideoEntries { get; set; } = null!;
        public DbSet<MatchResultObservation> MatchResultObservations { get; set; } = null!;
        public DbSet<OfficialDataSource> OfficialDataSources { get; set; } = null!;
        public DbSet<MatchResultCheck> MatchResultChecks { get; set; } = null!;
        public DbSet<MatchStatisticsCheck> MatchStatisticsChecks { get; set; } = null!;
        public DbSet<MatchStatisticObservation> MatchStatisticObservations { get; set; } = null!;
        public DbSet<MatchPredictionSnapshot> MatchPredictionSnapshots { get; set; } = null!;
        public DbSet<PredictionModelRun> PredictionModelRuns { get; set; } = null!;
        public DbSet<LeaguePredictionEligibility> LeaguePredictionEligibilities { get; set; } = null!;
        public DbSet<PredictionRecomputeRequest> PredictionRecomputeRequests { get; set; } = null!;
        public DbSet<PredictionScorecard> PredictionScorecards { get; set; } = null!;
        public DbSet<PredictionDiagnostic> PredictionDiagnostics { get; set; } = null!;
        public DbSet<MatchEventEntity> MatchEvents { get; set; }

        public DbSet<AIDecisionTrace> AIDecisionTraces { get; set; }
        public DbSet<StateTransitionLog> StateTransitionLogs { get; set; }
        public DbSet<AISelfInvalidationLog> AISelfInvalidationLogs { get; set; }

        public DbSet<LastExtendedContextKey> LastExtendedContextKeys { get; set; } = null!;
        public DbSet<AiSpeakTelemetry> AiSpeakTelemetries { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; } = null!;

        public DbSet<MatchRecommendationStat> MatchRecommendationStats { get; set; }

        public DbSet<UserInterestEvent> UserInterestEvents { get; set; }
        public DbSet<IntroAccess> IntroAccesses { get; set; }

        public DbSet<FeedInteractionEvent> FeedInteractionEvents { get; set; }
        public DbSet<UserContextMemory> UserContextMemories { get; set; }
        public DbSet<UserSessionInterest> UserSessionInterests { get; set; }

        public DbSet<MatchTrendStat> MatchTrendStats { get; set; }
        public DbSet<MatchNarrative> MatchNarratives { get; set; }
        public DbSet<MatchFeedState> MatchFeedStates { get; set; }
        public DbSet<MatchDiscoveryNode> MatchDiscoveryNodes { get; set; }
        public DbSet<MatchLiveEvent> MatchLiveEvents { get; set; }
        public DbSet<MatchExplorationStat> MatchExplorationStats { get; set; }

        public DbSet<SessionInteraction> SessionInteractions { get; set; }

        public DbSet<MatchRewardStats> MatchRewardStats { get; set; }

        public DbSet<UserInterestScore> UserInterestScores { get; set; }
        public DbSet<UserWeightProfile> UserWeightProfiles { get; set; }

        public DbSet<FeedScoreLog> FeedScoreLogs { get; set; }
        public DbSet<UserTasteProfile> UserTasteProfiles { get; set; }
        public DbSet<GlobalTrend> GlobalTrends { get; set; }

        public DbSet<AIWeightConfig> AIWeightConfigs { get; set; }
        public DbSet<UserAction> UserActions { get; set; }
        public DbSet<UserPreferenceWeights> UserPreferenceWeights { get; set; }
        public DbSet<MatchBanditStats> MatchBanditStats { get; set; }

        // ── Sprint 1: Lineup engine ──────────────────────────────────────────
        public DbSet<MatchLineup> MatchLineups { get; set; } = null!;
        public DbSet<MatchLineupPlayer> MatchLineupPlayers { get; set; } = null!;
        public DbSet<MatchPlayerStatus> MatchPlayerStatuses { get; set; } = null!;

        // ── Sprint 2: Standings & competition context ─────────────────────────
        public DbSet<LeagueStanding> LeagueStandings { get; set; } = null!;

        /// <summary>
        /// İç kaynaklı puan durumu projeksiyonu (lig+sezon başına TEK satır).
        /// Sağlayıcıdan gelen <see cref="LeagueStandings"/> tablosundan AYRIDIR ve onu
        /// değiştirmez; kimliği canonical takım id'sidir.
        /// </summary>
        public DbSet<LeagueStandingsSnapshot> LeagueStandingsSnapshots { get; set; } = null!;

        /// <summary>
        /// Lig sezonu metadata kayıtları — sezon başlangıcının TEK resmî kaynağı.
        /// İlk fikstür tarihi resmî başlangıç DEĞİLDİR (yalnız teşhis).
        /// </summary>
        public DbSet<LeagueSeason> LeagueSeasons { get; set; } = null!;

        /// <summary>Tekil fikstur istegi deneme defteri — restart-safe hiz siniri.</summary>
        public DbSet<FixtureRefreshAttempt> FixtureRefreshAttempts { get; set; } = null!;

        /// <summary>EMEKLI haber bagi (02.09.2026) — tablo korunuyor, beslenmiyor/okunmuyor.</summary>
        public DbSet<MatchPostContentLink> MatchPostContentLinks { get; set; } = null!;

        /// <summary>Yalniz DOGRULANMIS resmi mac videolari.</summary>
        public DbSet<MatchVideo> MatchVideos { get; set; } = null!;

        /// <summary>Bitmiş maçın kanonik olayları — bkz. <see cref="MatchEventRecord"/>.</summary>
        public DbSet<MatchEventRecord> MatchEventRecords { get; set; } = null!;

        /// <summary>Bitmiş maçın kanonik takım istatistikleri — bkz. <see cref="MatchTeamStatistic"/>.</summary>
        public DbSet<MatchTeamStatistic> MatchTeamStatistics { get; set; } = null!;
        public DbSet<CompetitionContext> CompetitionContexts { get; set; } = null!;
        public DbSet<LeagueExternalMapping> LeagueExternalMappings { get; set; } = null!;

        // ── Phase 6: api-football team season statistics (/teams/statistics) ──
        public DbSet<TeamSeasonStatistic> TeamSeasonStatistics { get; set; } = null!;

        // ── Phase 6 / Slice 2: api-football match prediction (/predictions) [AI-only] ──
        public DbSet<MatchPredictionSignal> MatchPredictionSignals { get; set; } = null!;

        // ── Phase 6 Final: api-football team profile (coach/venue/squad/transfers) [AI-only] ──
        public DbSet<TeamProfileSignal> TeamProfileSignals { get; set; } = null!;

        // Football Intelligence v1.0 — takım oyuncu-düzeyi zekâsı (PK internal TeamId, AI-only)
        public DbSet<TeamPlayerIntelligence> TeamPlayerIntelligences { get; set; } = null!;

        // ── Phase 7: Social Discovery — verified official accounts + canonical social posts ──
        public DbSet<OfficialSocialAccount> OfficialSocialAccounts { get; set; } = null!;
        public DbSet<SocialPost> SocialPosts { get; set; } = null!;

        // ── Sprint 3: Live match intelligence ─────────────────────────────────
        public DbSet<MatchLiveStats> MatchLiveStats { get; set; } = null!;
        public DbSet<MatchMomentumSnapshot> MatchMomentumSnapshots { get; set; } = null!;

        // ── Sprint 3b: Distributed ingestion lock ──────────────────────────────
        public DbSet<LiveIngestionLock> LiveIngestionLocks { get; set; } = null!;

        // ── Sprint 0: Fixture sync distributed lock ─────────────────────────────
        public DbSet<FixtureSyncLock> FixtureSyncLocks { get; set; } = null!;

        // ── Sprint 4: NABIZ feed intelligence ──────────────────────────────────
        public DbSet<MatchSocialFeedItem> MatchSocialFeedItems { get; set; } = null!;

        // ── R.8.1: Radar Source Engine — Source Registry ───────────────────────
        public DbSet<SourceDefinition> SourceDefinitions { get; set; } = null!;
        public DbSet<SourceStatus> SourceStatuses { get; set; } = null!;

        // ── R.8.5: Radar Source Engine — Staging ───────────────────────────────
        public DbSet<StagedSourceItem> StagedSourceItems { get; set; } = null!;

        // ── R.8.6: Radar Source Engine — Health ────────────────────────────────
        public DbSet<SourceHealthSnapshot> SourceHealthSnapshots { get; set; } = null!;

        // ── R.8.7: Radar Source Engine — Monitor ───────────────────────────────
        public DbSet<SourceMonitorSnapshot> SourceMonitorSnapshots { get; set; } = null!;

        // ── R.9.1: Radar Match Intelligence ────────────────────────────────────
        public DbSet<MatchIntelligenceSnapshot> MatchIntelligenceSnapshots { get; set; } = null!;

        // ── R.10.1: Radar News Intelligence ────────────────────────────────────
        public DbSet<NewsIntelligenceSnapshot> NewsIntelligenceSnapshots { get; set; } = null!;

        // ── R.11.1: Radar Odds Movement ────────────────────────────────────────
        public DbSet<OddsSnapshot> OddsSnapshots { get; set; } = null!;
        public DbSet<OddsMovementSnapshot> OddsMovementSnapshots { get; set; } = null!;

        // ── Real Market Odds — sağlayıcıdan gelen GERÇEK market oranları ────────
        public DbSet<MatchMarketOdd> MatchMarketOdds { get; set; } = null!;

        // ── R.12.1: Radar Commentary ───────────────────────────────────────────
        public DbSet<MatchCommentarySnapshot> MatchCommentarySnapshots { get; set; } = null!;

        // ── R.14.1: Radar Learning Events ──────────────────────────────────────
        public DbSet<LearningEvent> LearningEvents { get; set; } = null!;

        // ── Data Engine v1: Global Fixture Discovery ───────────────────────────
        public DbSet<Fixture> Fixtures { get; set; } = null!;

        // ── Data Engine v2: Global News Discovery ──────────────────────────────
        public DbSet<MatchNewsArticle> MatchNewsArticles { get; set; } = null!;

        /// <summary>Son Dakika haberlerinin dil karşılıkları — (ContentHash, Language) benzersiz.</summary>
        public DbSet<MatchNewsTranslation> MatchNewsTranslations { get; set; } = null!;

        // ── Data Engine v2.1: Match Intelligence Evidence Store ────────────────
        public DbSet<MatchEvidenceRecord> MatchEvidenceRecords { get; set; } = null!;

        // 🌐 FORMAX Canonical Domain — GDP tarafından beslenen eksik entity'ler (Domain tamamlama)
        public DbSet<Player> Players { get; set; } = null!;
        public DbSet<Competition> Competitions { get; set; } = null!;
        public DbSet<CompetitionStanding> CompetitionStandings { get; set; } = null!;
        public DbSet<Coach> Coaches { get; set; } = null!;
        public DbSet<Referee> Referees { get; set; } = null!;
        public DbSet<Venue> Venues { get; set; } = null!;

        // 🏛️ FORMAX Historical Data Platform (canlı/GDP'den izole tarihsel domain)
        public DbSet<HistoricalCompetition> HistoricalCompetitions { get; set; } = null!;
        public DbSet<HistoricalTeam> HistoricalTeams { get; set; } = null!;
        public DbSet<HistoricalMatch> HistoricalMatches { get; set; } = null!;
        public DbSet<HistoricalEloRating> HistoricalEloRatings { get; set; } = null!;
        public DbSet<MatchFeatureRecord> MatchFeatureRecords { get; set; } = null!;
        public DbSet<Lineup> Lineups { get; set; } = null!;
        public DbSet<MatchStatistics> MatchStatistics { get; set; } = null!;
        public DbSet<HeadToHead> HeadToHeads { get; set; } = null!;
        public DbSet<NewsArticle> NewsArticles { get; set; } = null!;
        public DbSet<MatchWeather> MatchWeathers { get; set; } = null!;
        public DbSet<Injury> Injuries { get; set; } = null!;
        public DbSet<Suspension> Suspensions { get; set; } = null!;
        public DbSet<Transfer> Transfers { get; set; } = null!;

        // 🌐 FORMAX GDP — yalnızca METADATA (maç verisi mevcut Domain Match aggregate'ında tutulur)
        public DbSet<Formax.Infrastructure.Persistence.GdpMatchLink> GdpMatchLinks { get; set; } = null!;
        public DbSet<Formax.Infrastructure.Persistence.GdpProviderFieldProvenance> GdpProviderFieldProvenances { get; set; } = null!;
        public DbSet<Formax.Infrastructure.Persistence.GdpConflictResolution> GdpConflictResolutions { get; set; } = null!;
        public DbSet<Formax.Infrastructure.Persistence.GdpProviderMatchReference> GdpProviderMatchReferences { get; set; } = null!;

        // ── Prediction Contract V1 (shadow mode) ───────────────────────────
        // INSERT-ONLY. Bu iki DbSet üzerinden UPDATE yapılmaz; değişmezlik uygulama, EF ve
        // veritabanı katmanlarında birlikte korunur.
        public DbSet<Formax.Domain.Entities.Prediction> Predictions { get; set; } = null!;
        public DbSet<Formax.Domain.Entities.PredictionSettlement> PredictionSettlements { get; set; } = null!;

        // ── Shadow B (NEWS_ADJUSTED deney hattı) ───────────────────────────
        // AYRI TABLOLAR. Shadow A'nın Predictions/PredictionSettlements şeması, kısıtları ve
        // tetikleyicileri DEĞİŞMEZ — B kendi tablolarına yazar, A'yı yalnız okur.
        public DbSet<Formax.Domain.Entities.ShadowBPrediction> ShadowBPredictions { get; set; } = null!;
        public DbSet<Formax.Domain.Entities.ShadowBPredictionEvidence> ShadowBPredictionEvidence { get; set; } = null!;
        public DbSet<Formax.Domain.Entities.ShadowBPredictionSettlement> ShadowBPredictionSettlements { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Prediction Contract V1 ─────────────────────────────────────
            modelBuilder.Entity<Formax.Domain.Entities.Prediction>(entity =>
            {
                // EF8'e tablonun TETİKLEYİCİSİ olduğu bildirilir.
                //
                // Neden gerekli: SQL Server, tetikleyicili bir tabloya INTO'suz `OUTPUT` yan
                // tümceli DML kabul etmez. EF bunu bilmezse UPDATE/DELETE için ürettiği SQL
                // veritabanına ULAŞMADAN hata verir — sonuç yine "engellendi" olur ama yanlış
                // sebeple: değişmezliği trigger değil, bir uyumsuzluk sağlıyor olur. Bildirimle
                // birlikte EF trigger-uyumlu SQL üretir, UPDATE gerçekten trigger'a ulaşır ve
                // trigger'ın kendi mesajıyla reddedilir. INSERT yolu etkilenmez.
                entity.ToTable("Predictions", tb =>
                {
                    tb.HasTrigger("TR_Predictions_NoUpdate");
                    tb.HasTrigger("TR_Predictions_NoDelete");
                });
                entity.HasKey(x => x.Sequence);
                entity.Property(x => x.Sequence).ValueGeneratedOnAdd();

                entity.HasIndex(x => x.PredictionId).IsUnique();
                entity.HasIndex(x => new { x.MatchId, x.Sequence }).HasDatabaseName("IX_Predictions_Match_Sequence");
                entity.HasIndex(x => x.MatchDate);
                entity.HasIndex(x => new { x.ModelVersion, x.TeamStrengthVersion, x.GateVersion, x.CalibrationVersion })
                      .HasDatabaseName("IX_Predictions_Versions");

                entity.Property(x => x.PredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.CanonicalMatchId).HasMaxLength(32);
                entity.Property(x => x.ModelVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.TeamStrengthVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.GateVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.CalibrationVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ConfidenceClass).HasMaxLength(16).IsRequired();
                entity.Property(x => x.GateStatus).HasMaxLength(16).IsRequired();
                entity.Property(x => x.GateReason).HasMaxLength(256).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(32).IsRequired();

                // Olasılık ONDALIK saklanır. decimal DEĞİL: decimal sessizce yuvarlar ve
                // sunum katmanına ait bir kararı şemaya taşır.
                entity.Property(x => x.HomeProbability).HasColumnType("float");
                entity.Property(x => x.DrawProbability).HasColumnType("float");
                entity.Property(x => x.AwayProbability).HasColumnType("float");

                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<Formax.Domain.Entities.PredictionSettlement>(entity =>
            {
                entity.ToTable("PredictionSettlements", tb =>
                {
                    tb.HasTrigger("TR_PredictionSettlements_NoUpdate");
                    tb.HasTrigger("TR_PredictionSettlements_NoDelete");
                });
                // PK = PredictionId → bir tahmin iki kez settle EDİLEMEZ (şema düzeyinde).
                entity.HasKey(x => x.PredictionId);
                entity.Property(x => x.PredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.ActualResult).HasMaxLength(8).IsRequired();

                entity.HasOne(x => x.Prediction)
                      .WithOne(p => p.Settlement)
                      .HasPrincipalKey<Formax.Domain.Entities.Prediction>(p => p.PredictionId)
                      .HasForeignKey<Formax.Domain.Entities.PredictionSettlement>(x => x.PredictionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Shadow B — NEWS_ADJUSTED deney hattı (INSERT-ONLY) ─────────
            modelBuilder.Entity<Formax.Domain.Entities.ShadowBPrediction>(entity =>
            {
                entity.ToTable("ShadowBPredictions", tb =>
                {
                    tb.HasTrigger("TR_ShadowBPredictions_NoUpdate");
                    tb.HasTrigger("TR_ShadowBPredictions_NoDelete");
                });
                entity.HasKey(x => x.Sequence);
                entity.Property(x => x.Sequence).ValueGeneratedOnAdd();

                entity.HasIndex(x => x.PredictionId).IsUnique();
                entity.HasIndex(x => new { x.MatchId, x.Sequence }).HasDatabaseName("IX_ShadowBPredictions_Match_Sequence");
                entity.HasIndex(x => x.BasePredictionId).HasDatabaseName("IX_ShadowBPredictions_Base");
                entity.HasIndex(x => x.MatchDate);

                entity.Property(x => x.PredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.BasePredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.Variant).HasMaxLength(32).IsRequired();
                entity.Property(x => x.CanonicalMatchId).HasMaxLength(32);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(32).IsRequired();
                entity.Property(x => x.ModelVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.TeamStrengthVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.GateVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.CalibrationVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.AdjustmentVersion).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ConfidenceClass).HasMaxLength(16).IsRequired();
                entity.Property(x => x.GateStatus).HasMaxLength(16).IsRequired();
                entity.Property(x => x.GateReason).HasMaxLength(256).IsRequired();
                entity.Property(x => x.AdjustmentReason).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(32).IsRequired();

                foreach (var p in new[] { nameof(Formax.Domain.Entities.ShadowBPrediction.HomeProbability),
                                          nameof(Formax.Domain.Entities.ShadowBPrediction.DrawProbability),
                                          nameof(Formax.Domain.Entities.ShadowBPrediction.AwayProbability),
                                          nameof(Formax.Domain.Entities.ShadowBPrediction.BaseHomeProbability),
                                          nameof(Formax.Domain.Entities.ShadowBPrediction.BaseDrawProbability),
                                          nameof(Formax.Domain.Entities.ShadowBPrediction.BaseAwayProbability) })
                    entity.Property(p).HasColumnType("float");

                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<Formax.Domain.Entities.ShadowBPredictionEvidence>(entity =>
            {
                entity.ToTable("ShadowBPredictionEvidence", tb =>
                {
                    tb.HasTrigger("TR_ShadowBPredictionEvidence_NoUpdate");
                    tb.HasTrigger("TR_ShadowBPredictionEvidence_NoDelete");
                });
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();

                entity.Property(x => x.PredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.EvidenceContentHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
                entity.Property(x => x.RelatedTeam).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Side).HasMaxLength(8).IsRequired();
                entity.Property(x => x.Source).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Weight).HasColumnType("float");

                entity.HasIndex(x => x.PredictionId).HasDatabaseName("IX_ShadowBPredictionEvidence_Prediction");
                entity.HasIndex(x => new { x.PredictionId, x.EvidenceId })
                      .IsUnique().HasDatabaseName("UX_ShadowBPredictionEvidence_Prediction_Evidence");

                entity.HasOne(x => x.Prediction)
                      .WithMany(p => p.Evidence)
                      .HasPrincipalKey(p => p.PredictionId)
                      .HasForeignKey(x => x.PredictionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Formax.Domain.Entities.ShadowBPredictionSettlement>(entity =>
            {
                entity.ToTable("ShadowBPredictionSettlements", tb =>
                {
                    tb.HasTrigger("TR_ShadowBPredictionSettlements_NoUpdate");
                    tb.HasTrigger("TR_ShadowBPredictionSettlements_NoDelete");
                });
                // PK = PredictionId → bir B tahmini iki kez settle EDİLEMEZ (şema düzeyinde).
                entity.HasKey(x => x.PredictionId);
                entity.Property(x => x.PredictionId).HasMaxLength(24).IsRequired();
                entity.Property(x => x.ActualResult).HasMaxLength(8).IsRequired();

                entity.HasOne(x => x.Prediction)
                      .WithOne(p => p.Settlement)
                      .HasPrincipalKey<Formax.Domain.Entities.ShadowBPrediction>(p => p.PredictionId)
                      .HasForeignKey<Formax.Domain.Entities.ShadowBPredictionSettlement>(x => x.PredictionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Data Engine v1: Fixtures (FORMAX_MATCH_ID benzersiz kimlik) ─────
            modelBuilder.Entity<Fixture>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.FormaxMatchId).IsUnique();
                entity.HasIndex(x => x.KickoffUtc);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Country).HasMaxLength(96);
                entity.Property(x => x.League).HasMaxLength(160);
                entity.Property(x => x.Season).HasMaxLength(32);
                entity.Property(x => x.Round).HasMaxLength(64);
                entity.Property(x => x.HomeTeam).HasMaxLength(160).IsRequired();
                entity.Property(x => x.AwayTeam).HasMaxLength(160).IsRequired();
                entity.Property(x => x.Venue).HasMaxLength(160);
                entity.Property(x => x.Status).HasMaxLength(32);
                entity.Property(x => x.Sources).HasMaxLength(256);
            });

            // ── Data Engine v2: MatchNewsArticles (FORMAX_MATCH_ID altında haber) ──
            modelBuilder.Entity<MatchNewsArticle>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.ContentHash).IsUnique();
                entity.HasIndex(x => x.FormaxMatchId);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Headline).HasMaxLength(512).IsRequired();
                entity.Property(x => x.Summary).HasMaxLength(1024);
                entity.Property(x => x.Url).HasMaxLength(1024);
                entity.Property(x => x.Sources).HasMaxLength(512);
                entity.Property(x => x.Language).HasMaxLength(8);
                entity.Property(x => x.Clusters).HasMaxLength(256);
                entity.Property(x => x.ContentHash).HasMaxLength(48).IsRequired();
            });

            // ── Son Dakika: haber çevirisi (ContentHash + dil) ─────────────────
            modelBuilder.Entity<MatchNewsTranslation>(entity =>
            {
                entity.HasKey(x => x.Id);
                // Aynı haber + aynı dil ikinci kez çevrilmez.
                entity.HasIndex(x => new { x.ContentHash, x.Language }).IsUnique();
                entity.Property(x => x.ContentHash).HasMaxLength(48).IsRequired();
                entity.Property(x => x.Language).HasMaxLength(8).IsRequired();
                entity.Property(x => x.Headline).HasMaxLength(512).IsRequired();
                entity.Property(x => x.Summary).HasMaxLength(1024);
            });

            // ── Data Engine v2.1: Evidence Store ───────────────────────────────
            modelBuilder.Entity<MatchEvidenceRecord>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.ContentHash).IsUnique();
                entity.HasIndex(x => x.FormaxMatchId);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Type).HasMaxLength(48);
                entity.Property(x => x.Cluster).HasMaxLength(256);
                entity.Property(x => x.Source).HasMaxLength(160);
                entity.Property(x => x.Headline).HasMaxLength(512).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(48).IsRequired();
            });

            // ── R.8.1: Radar Source Registry ───────────────────────────────────
            modelBuilder.Entity<SourceDefinition>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.SourceKey).IsUnique();
                entity.Property(x => x.SourceKey).HasMaxLength(128).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(256);
                entity.Property(x => x.FailoverGroup).HasMaxLength(128);
                entity.Property(x => x.Type).HasConversion<int>();
                entity.Property(x => x.Category).HasConversion<int>();
                entity.Property(x => x.Lifecycle).HasConversion<int>();
            });

            modelBuilder.Entity<SourceStatus>(entity =>
            {
                entity.HasKey(x => x.SourceId);
                entity.Property(x => x.SourceId).ValueGeneratedNever();
                entity.Property(x => x.CircuitState).HasConversion<int>();
            });

            // R.8.5 — staging mapping (this codebase configures inline, so the
            // configuration class must be applied explicitly to take effect).
            modelBuilder.ApplyConfiguration(
                new Formax.Infrastructure.Configurations.StagedSourceItemConfiguration());

            // R.8.6 — Radar source health snapshot.
            modelBuilder.Entity<SourceHealthSnapshot>(entity =>
            {
                entity.HasKey(x => x.SourceId);
                entity.Property(x => x.SourceId).ValueGeneratedNever();
                entity.Property(x => x.SourceKey).HasMaxLength(128);
                entity.Property(x => x.Status).HasConversion<int>();
            });

            // R.8.7 — Radar source monitor snapshot.
            modelBuilder.Entity<SourceMonitorSnapshot>(entity =>
            {
                entity.HasKey(x => x.SourceId);
                entity.Property(x => x.SourceId).ValueGeneratedNever();
                entity.Property(x => x.SourceKey).HasMaxLength(128);
                entity.Property(x => x.Reason).HasMaxLength(512);
                entity.Property(x => x.Status).HasConversion<int>();
                entity.Property(x => x.AlertType).HasConversion<int>();
            });

            // R.9.1 — Radar match intelligence snapshot (+ R.9.6 importance).
            modelBuilder.Entity<MatchIntelligenceSnapshot>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.Status).HasConversion<int>();
                entity.Property(x => x.PrimarySignalType).HasConversion<int>();
                entity.Property(x => x.Summary).HasMaxLength(512);
                entity.Property(x => x.ImportanceLevel).HasConversion<int>();
                entity.Property(x => x.NewsImpactLevel).HasConversion<int>();
                entity.Property(x => x.SyntheticSignalLevel).HasConversion<int>();
                entity.Property(x => x.SyntheticDirection).HasConversion<int>();
            });

            // R.10.1 — Radar news intelligence snapshot (+ R.10.3 impact).
            modelBuilder.Entity<NewsIntelligenceSnapshot>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.ImpactLevel).HasConversion<int>();
            });

            // R.11.1 — Radar odds movement.
            modelBuilder.Entity<OddsSnapshot>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.MatchId, x.CapturedAtUtc });
            });
            // Real Market Odds — market başına TEK satır (upsert hedefi).
            modelBuilder.Entity<MatchMarketOdd>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.MarketKey).HasMaxLength(40).IsRequired();
                entity.Property(x => x.BookmakerName).HasMaxLength(80);
                entity.Property(x => x.Odd).HasColumnType("decimal(8,3)");
                entity.Property(x => x.PreviousOdd).HasColumnType("decimal(8,3)");
                entity.HasIndex(x => new { x.MatchId, x.MarketKey }).IsUnique();
            });

            modelBuilder.Entity<OddsMovementSnapshot>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.MatchId, x.ComputedAtUtc });
                entity.Property(x => x.Direction).HasConversion<int>();
                entity.Property(x => x.Level).HasConversion<int>();
            });

            // R.12.1 — Radar match commentary.
            modelBuilder.Entity<MatchCommentarySnapshot>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.Headline).HasMaxLength(256);
                entity.Property(x => x.Summary).HasMaxLength(1024);
                entity.Property(x => x.Tone).HasConversion<int>();
                entity.Property(x => x.Visibility).HasConversion<int>();
            });

            // R.14.1 — Radar learning event.
            modelBuilder.Entity<LearningEvent>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
                entity.Property(x => x.EventType).HasConversion<int>();
                entity.Property(x => x.Source).HasMaxLength(32);
            });

            // 🌐 FORMAX canonical entity yapılandırması (maç-ilişkili alanlar indekslenir)
            modelBuilder.Entity<Lineup>().Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<Lineup>().HasIndex(x => x.FormaxMatchId);
            modelBuilder.Entity<MatchStatistics>().Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<MatchStatistics>().HasIndex(x => x.FormaxMatchId);
            modelBuilder.Entity<HeadToHead>().Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<HeadToHead>().HasIndex(x => x.FormaxMatchId);
            modelBuilder.Entity<NewsArticle>().Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<MatchWeather>().Property(x => x.FormaxMatchId).HasMaxLength(64);

            // 🌐 FORMAX GDP metadata yapılandırması (maç verisi Domain Match'te; burada yalnızca metadata)
            modelBuilder.Entity<Formax.Infrastructure.Persistence.GdpMatchLink>(entity =>
            {
                entity.HasKey(x => x.FormaxMatchId);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(64);
                entity.Property(x => x.Round).HasMaxLength(64);
                entity.HasIndex(x => x.MatchId);
                entity.HasMany(x => x.Provenance).WithOne().HasForeignKey(x => x.FormaxMatchId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(x => x.ConflictResolutions).WithOne().HasForeignKey(x => x.FormaxMatchId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(x => x.ProviderReferences).WithOne().HasForeignKey(x => x.FormaxMatchId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Formax.Infrastructure.Persistence.GdpProviderFieldProvenance>()
                .Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<Formax.Infrastructure.Persistence.GdpConflictResolution>()
                .Property(x => x.FormaxMatchId).HasMaxLength(64);
            modelBuilder.Entity<Formax.Infrastructure.Persistence.GdpProviderMatchReference>(entity =>
            {
                entity.Property(x => x.FormaxMatchId).HasMaxLength(64);
                entity.HasIndex(x => new { x.FormaxMatchId, x.ProviderName, x.ProviderMatchId }).IsUnique();
            });

            modelBuilder.Entity<UserStats>()
                .HasKey(x => x.UserId);

            modelBuilder.Entity<UserStats>()
                .Property(x => x.UserId)
                .ValueGeneratedNever();

            modelBuilder.Entity<UserConfidence>()
                .HasKey(x => x.UserId);

            modelBuilder.Entity<UserConfidence>()
                .Property(x => x.UserId)
                .ValueGeneratedNever();

            modelBuilder.Entity<UserPickStats>()
                .HasKey(x => new { x.UserId, x.PickLabel });

            modelBuilder.Entity<MatchSapmaSnapshot>()
                .HasKey(x => x.MatchId);

            modelBuilder.Entity<MatchSapmaSnapshot>()
                .Property(x => x.MatchId)
                .ValueGeneratedNever();

            modelBuilder.Entity<Match>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Status).IsRequired();
                entity.Property(x => x.League)
                      .IsRequired()
                      .HasMaxLength(120);
                // Sağlayıcının gerçek tur/aşama adı — maç türünün kaynağı.
                entity.Property(x => x.Round).HasMaxLength(120);

                // ── Sprint 0: fast upsert lookup by external provider ID ──────
                entity.HasIndex(x => x.ExternalMatchId)
                      .HasDatabaseName("IX_Matches_ExternalMatchId")
                      .IsUnique();
                entity.HasOne(x => x.Competition)
                      .WithMany()
                      .HasForeignKey(x => x.CompetitionId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.CanonicalVenue)
                      .WithMany()
                      .HasForeignKey(x => x.VenueId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Competition>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
                entity.HasIndex(x => x.Name).IsUnique();
                entity.Property(x => x.Country).HasMaxLength(80);
            });

            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
                entity.HasIndex(x => x.Name).IsUnique();
                entity.Property(x => x.City).HasMaxLength(120);
                entity.Property(x => x.Country).HasMaxLength(80);
            });

            // 🏛️ Historical Data Platform yapılandırması (dedup için benzersiz kaynak anahtarları)
            modelBuilder.Entity<HistoricalCompetition>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Division).IsRequired().HasMaxLength(16);
                entity.HasIndex(x => x.Division).IsUnique();
                entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
                entity.Property(x => x.Country).HasMaxLength(80);
            });

            modelBuilder.Entity<HistoricalTeam>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
                entity.Property(x => x.NormalizedKey).IsRequired().HasMaxLength(160);
                entity.HasIndex(x => x.NormalizedKey).IsUnique();
                entity.Property(x => x.Country).HasMaxLength(80);
            });

            modelBuilder.Entity<HistoricalMatch>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).IsRequired().HasMaxLength(200);
                entity.HasIndex(x => x.SourceKey).IsUnique();
                entity.HasIndex(x => x.MatchDate);
                entity.Property(x => x.FTResult).HasMaxLength(4);
                entity.Property(x => x.HTResult).HasMaxLength(4);
                entity.Property(x => x.MatchTime).HasMaxLength(8);
                entity.HasOne<HistoricalCompetition>().WithMany().HasForeignKey(x => x.HistoricalCompetitionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<HistoricalTeam>().WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<HistoricalTeam>().WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<HistoricalEloRating>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).IsRequired().HasMaxLength(200);
                entity.HasIndex(x => x.SourceKey).IsUnique();
                entity.Property(x => x.Club).IsRequired().HasMaxLength(160);
                entity.Property(x => x.Country).HasMaxLength(80);
                entity.HasIndex(x => new { x.Club, x.Date });
                entity.HasOne<HistoricalTeam>().WithMany().HasForeignKey(x => x.HistoricalTeamId).OnDelete(DeleteBehavior.SetNull);
            });

            // 🧮 Feature Store — maç başına tek feature kaydı (Probability Engine'in tek kaynağı)
            modelBuilder.Entity<MatchFeatureRecord>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.HistoricalMatchId).IsUnique();
                entity.HasIndex(x => x.MatchDate);
                entity.Property(x => x.FeatureHash).IsRequired().HasMaxLength(64);
                entity.Property(x => x.FeaturesJson).IsRequired();
                entity.HasOne<HistoricalMatch>().WithMany().HasForeignKey(x => x.HistoricalMatchId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CompetitionStanding>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.CompetitionName).IsRequired().HasMaxLength(120);
                entity.Property(x => x.TeamName).IsRequired().HasMaxLength(120);
                entity.HasIndex(x => new { x.CompetitionName, x.TeamName }).IsUnique();
            });

            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired();

                // ── Sprint 0: fast upsert lookup by external provider ID ──────
                entity.HasIndex(x => x.ExternalTeamId)
                      .HasDatabaseName("IX_Teams_ExternalTeamId")
                      .IsUnique();
            });

            modelBuilder.Entity<Match>()
                .HasOne(m => m.HomeTeam)
                .WithMany(t => t.HomeMatches)
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Match>()
                .HasOne(m => m.AwayTeam)
                .WithMany(t => t.AwayMatches)
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            // ── KULLANICI SEÇİMLERİ ─────────────────────────────────────────────
            //
            // İNDEKS GENİŞLETİLDİ (06.09.2026): eski benzersizlik (UserId + MatchId)
            // bir kullanıcıya maç başına TEK seçim hakkı veriyordu. Ürün kararı ise
            // birden fazla UYUMLU seçime izin verir ("2.5 Alt" + "Karşılıklı Gol Var"
            // + "Çifte Şans (1X)"). Benzersizlik artık MARKET düzeyindedir: aynı
            // kullanıcı aynı maçta aynı marketi iki kez kaydedemez, ama farklı
            // marketleri birlikte seçebilir. Çelişkili seçimler (aynı market grubu)
            // uygulama katmanında elenir — bkz. PickMarketGroups.
            //
            // Veri kaybı YOKTUR: kısıt daraltılmadı, GENİŞLETİLDİ; eski indeksin
            // kabul ettiği her satır yeni indekste de geçerlidir.
            modelBuilder.Entity<UserPick>(entity =>
            {
                entity.Property(x => x.MarketKey).HasMaxLength(32);
                entity.Property(x => x.MarketGroup).HasMaxLength(32);
                entity.Property(x => x.ModelVersions).HasMaxLength(200);
                entity.Property(x => x.ModelFingerprint).HasMaxLength(120);
                entity.Property(x => x.SelectionStatus).HasMaxLength(24);
                entity.Property(x => x.SettlementNote).HasMaxLength(200);
                entity.Property(x => x.OddAtSelection).HasPrecision(10, 3);

                entity.HasIndex(x => new { x.UserId, x.MatchId, x.MarketKey })
                      .IsUnique()
                      .HasDatabaseName("UX_UserPicks_User_Match_Market");

                // Tahminlerim ekranı "bu kullanıcının tüm seçimleri" diye sorar.
                entity.HasIndex(x => x.UserId).HasDatabaseName("IX_UserPicks_User");
            });

            // 🔥 EKLEDİK (KRİTİK)
            modelBuilder.Entity<MatchBanditStats>()
                .HasKey(x => x.MatchId);

            modelBuilder.Entity<UserPreferenceWeights>()
                .HasKey(x => x.UserId);

            // ── Sprint 2: Standings & competition context ────────────────────

            // LeagueStanding — composite PK (LeagueId, SeasonYear, TeamId)
            modelBuilder.Entity<LeagueStanding>(entity =>
            {
                entity.HasKey(x => new { x.LeagueId, x.SeasonYear, x.TeamId });
                entity.Property(x => x.TeamName).HasMaxLength(120).IsRequired();
                entity.Property(x => x.Form).HasMaxLength(20);
                entity.Ignore(x => x.GoalDifference);   // computed property — not stored
                entity.HasIndex(x => new { x.LeagueId, x.SeasonYear });
            });

            // LeagueSeason — sezon metadata (PK: LeagueId + SeasonYear).
            modelBuilder.Entity<LeagueSeason>(entity =>
            {
                entity.HasKey(x => new { x.LeagueId, x.SeasonYear });
                entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Notes).HasMaxLength(400);
            });

            // Kimlik: fikstur + amac + UTC gun. Gun anahtarda oldugu icin gunluk tavan TAM
            // sayilir; sogutma ise gunlerden bagimsiz olarak son deneme anindan hesaplanir.
            modelBuilder.Entity<FixtureRefreshAttempt>(entity =>
            {
                entity.HasKey(x => new { x.ExternalMatchId, x.Purpose, x.DayUtc });
                entity.Property(x => x.ExternalMatchId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
                entity.Property(x => x.LastOutcome).HasMaxLength(32);
                entity.HasIndex(x => new { x.Purpose, x.DayUtc }).HasDatabaseName("IX_FixtureRefreshAttempts_Purpose_Day");
            });

            modelBuilder.Entity<MatchPostContentLink>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
                entity.Property(x => x.Category).HasMaxLength(32).IsRequired();
                entity.Property(x => x.MatchReason).HasMaxLength(256);
                // Ayni makale ayni maca IKI KEZ baglanamaz.
                entity.HasIndex(x => new { x.MatchId, x.ContentHash }).IsUnique()
                      .HasDatabaseName("UX_MatchPostContentLinks_Match_Content");
            });

            modelBuilder.Entity<MatchVideo>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ExternalVideoId).HasMaxLength(128).IsRequired();
                entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
                entity.Property(x => x.OfficialPublisher).HasMaxLength(120).IsRequired();
                entity.Property(x => x.SourcePageUrl).HasMaxLength(600).IsRequired();
                entity.Property(x => x.EmbedUrl).HasMaxLength(600);
                entity.Property(x => x.ThumbnailUrl).HasMaxLength(600);
                entity.Property(x => x.VideoType).HasMaxLength(40).IsRequired();
                entity.Property(x => x.VerificationStatus).HasMaxLength(32).IsRequired();
                entity.Property(x => x.VerificationNote).HasMaxLength(400);
                entity.Property(x => x.DiscoveryProvenance).HasMaxLength(24);
                entity.Property(x => x.EvidencePageUrl).HasMaxLength(1000);
                entity.Property(x => x.EvidenceSourceKey).HasMaxLength(80);
                entity.Property(x => x.RejectionReason).HasMaxLength(64);
                entity.Property(x => x.AvailableCountries).HasMaxLength(1000);
                entity.Property(x => x.EventPlayer).HasMaxLength(120);
                entity.Property(x => x.EventTeam).HasMaxLength(120);
                // AYNI VIDEO IKI KEZ YAZILAMAZ — ne kaynak kimligiyle, ne kanonik adresle.
                entity.HasIndex(x => new { x.MatchId, x.ExternalVideoId }).IsUnique()
                      .HasDatabaseName("UX_MatchVideos_Match_Video");
                entity.HasIndex(x => new { x.MatchId, x.SourcePageUrl }).IsUnique()
                      .HasDatabaseName("UX_MatchVideos_Match_SourcePage");
                // Okuma yolu her zaman "bu macin oynatilabilir videolari" diye sorar.
                entity.HasIndex(x => new { x.MatchId, x.CanPlayInApp })
                      .HasDatabaseName("IX_MatchVideos_Match_Playable");
            });

            // ── BİTMİŞ MAÇ OLAYLARI — kanonik kayıt (canlı akıştan bağımsız) ─────
            modelBuilder.Entity<MatchEventRecord>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ProviderEventId).HasMaxLength(160).IsRequired();
                entity.Property(x => x.EventType).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Detail).HasMaxLength(120);
                entity.Property(x => x.Comments).HasMaxLength(400);
                entity.Property(x => x.TeamName).HasMaxLength(120);
                entity.Property(x => x.PlayerName).HasMaxLength(160);
                entity.Property(x => x.AssistName).HasMaxLength(160);
                entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
                // AYNI OLAY İKİ KEZ YAZILAMAZ. ProviderEventId, (fikstür+dakika+tür+
                // oyuncu) imzasından türetilen KARARLI anahtardır: aynı yanıt ikinci kez
                // işlense aynı anahtarı üretir ve bu indeks yazımı reddeder.
                entity.HasIndex(x => new { x.MatchId, x.ProviderEventId }).IsUnique()
                      .HasDatabaseName("UX_MatchEventRecords_Match_Event");
                // Okuma yolu her zaman "bu maçın olayları, dakika sırasıyla" diye sorar.
                entity.HasIndex(x => new { x.MatchId, x.Minute })
                      .HasDatabaseName("IX_MatchEventRecords_Match_Minute");
            });

            // ── BİTMİŞ MAÇ TAKIM İSTATİSTİĞİ — maç başına iki satır ─────────────
            modelBuilder.Entity<MatchTeamStatistic>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Side).HasMaxLength(8).IsRequired();
                entity.Property(x => x.TeamName).HasMaxLength(120);
                entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
                // Bir maçın bir tarafı için TEK satır olur; ikinci çekim günceller, eklemez.
                entity.HasIndex(x => new { x.MatchId, x.Side }).IsUnique()
                      .HasDatabaseName("UX_MatchTeamStatistics_Match_Side");
            });

            // LeagueStandingsSnapshot — İÇ KAYNAKLI projeksiyon; lig+sezon başına TEK satır.
            modelBuilder.Entity<LeagueStandingsSnapshot>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
                entity.Property(x => x.RankingRuleId).HasMaxLength(64);
                entity.Property(x => x.RowsJson).IsRequired();
                entity.HasIndex(x => new { x.LeagueId, x.SeasonYear }).IsUnique();
            });

            // TeamSeasonStatistic — composite PK (LeagueId, SeasonYear, TeamId=external)
            modelBuilder.Entity<TeamSeasonStatistic>(entity =>
            {
                entity.HasKey(x => new { x.LeagueId, x.SeasonYear, x.TeamId });
                entity.Property(x => x.TeamName).HasMaxLength(120).IsRequired();
                entity.Property(x => x.Form).HasMaxLength(40);
                entity.HasIndex(x => new { x.LeagueId, x.SeasonYear });
            });

            // MatchPredictionSignal — PK MatchId (per-match; AI-only)
            modelBuilder.Entity<MatchPredictionSignal>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.ExternalMatchId).HasMaxLength(64);
                entity.Property(x => x.WinnerName).HasMaxLength(160);
                entity.Property(x => x.WinnerSide).HasMaxLength(8);
                entity.Property(x => x.Advice).HasMaxLength(512);
                entity.Property(x => x.UnderOver).HasMaxLength(32);
            });

            // TeamProfileSignal — PK TeamId (internal; per-team; AI-only)
            modelBuilder.Entity<TeamProfileSignal>(entity =>
            {
                entity.HasKey(x => x.TeamId);
                entity.Property(x => x.TeamId).ValueGeneratedNever();
                entity.Property(x => x.ExternalTeamId).HasMaxLength(32);
                entity.Property(x => x.CoachName).HasMaxLength(160);
                entity.Property(x => x.VenueName).HasMaxLength(200);
                entity.Property(x => x.VenueCity).HasMaxLength(120);
                entity.Property(x => x.VenueSurface).HasMaxLength(40);
            });

            // TeamPlayerIntelligence — PK TeamId (internal; per-team; Football Intelligence v1.0)
            modelBuilder.Entity<TeamPlayerIntelligence>(entity =>
            {
                entity.HasKey(x => x.TeamId);
                entity.Property(x => x.TeamId).ValueGeneratedNever();
                entity.Property(x => x.ExternalTeamId).HasMaxLength(32);
                entity.Property(x => x.TopScorerName).HasMaxLength(160);
                entity.Property(x => x.TopAssistName).HasMaxLength(160);
                entity.Property(x => x.KeyPlayerName).HasMaxLength(160);
                entity.Property(x => x.MinutesLeaderName).HasMaxLength(160);
                entity.Property(x => x.InjuredNames).HasMaxLength(1024);
                entity.Property(x => x.DefenseLeaderName).HasMaxLength(160);
                entity.Property(x => x.MidfieldBrainName).HasMaxLength(160);
                entity.Property(x => x.ShotsLeaderName).HasMaxLength(160);
                entity.Property(x => x.KeyPassLeaderName).HasMaxLength(160);
                entity.Property(x => x.CardRiskName).HasMaxLength(160);
            });

            // OfficialSocialAccount — verified official accounts registry
            modelBuilder.Entity<OfficialSocialAccount>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
                entity.Property(x => x.ExternalTeamId).HasMaxLength(32);
                entity.Property(x => x.Platform).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Handle).HasMaxLength(120).IsRequired();
                entity.Property(x => x.FeedUrl).HasMaxLength(512);
                entity.Property(x => x.AccountName).HasMaxLength(160);
                entity.HasIndex(x => new { x.Platform, x.Handle }).IsUnique();
                entity.HasIndex(x => x.ExternalTeamId);
            });

            // SocialPost — Canonical Social (per-match; feeds AI + UI)
            modelBuilder.Entity<SocialPost>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FormaxMatchId).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Platform).HasMaxLength(16).IsRequired();
                entity.Property(x => x.AccountHandle).HasMaxLength(120);
                entity.Property(x => x.AccountName).HasMaxLength(160);
                entity.Property(x => x.Headline).HasMaxLength(512).IsRequired();
                entity.Property(x => x.Summary).HasMaxLength(1024);
                entity.Property(x => x.Url).HasMaxLength(1000);
                entity.Property(x => x.SignalType).HasMaxLength(48);
                entity.Property(x => x.ContentHash).HasMaxLength(48).IsRequired();
                entity.HasIndex(x => x.ContentHash).IsUnique();
                entity.HasIndex(x => x.FormaxMatchId);
            });

            // CompetitionContext — MatchId is PK
            modelBuilder.Entity<CompetitionContext>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.CompetitionType).HasMaxLength(20).IsRequired();
                entity.Property(x => x.StageName).HasMaxLength(120);
                entity.Property(x => x.ContextHeadline).HasMaxLength(200);
                entity.Property(x => x.ContextSummary).HasMaxLength(500);
            });

            // LeagueExternalMapping — LeagueId is PK (simple int)
            modelBuilder.Entity<LeagueExternalMapping>(entity =>
            {
                entity.HasKey(x => x.LeagueId);
                entity.Property(x => x.LeagueId).ValueGeneratedNever();
                entity.Property(x => x.ExternalLeagueId).HasMaxLength(50).IsRequired();
                entity.Property(x => x.LeagueName).HasMaxLength(120);
            });

            // ── Sprint 1: Lineup engine ──────────────────────────────────────

            // MatchLineup — one row per match, MatchId is PK + explicit FK to Matches
            modelBuilder.Entity<MatchLineup>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();

                entity.HasOne(x => x.Match)
                      .WithMany()
                      .HasForeignKey(x => x.MatchId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Kaynak kimliği (11.09.2026 · additive, hepsi null olabilir).
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(32);
                entity.Property(x => x.HomeCoach).HasMaxLength(128);
                entity.Property(x => x.AwayCoach).HasMaxLength(128);
                entity.Property(x => x.Provider).HasMaxLength(32);
                // Resmî kaynak kanıtı (additive).
                entity.Property(x => x.SourceKey).HasMaxLength(120);
                entity.Property(x => x.SourceUrl).HasMaxLength(1000);
                entity.Property(x => x.RawContentHash).HasMaxLength(64);
                entity.Property(x => x.VerificationStatus).HasMaxLength(32);
            });

            // ── RESMÎ KAYNAK ALTYAPISI ───────────────────────────────────────
            modelBuilder.Entity<OfficialSourceFetch>(entity =>
            {
                entity.ToTable("OfficialSourceFetches");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.Provider).HasMaxLength(80).IsRequired();
                entity.Property(x => x.Host).HasMaxLength(200).IsRequired();
                entity.Property(x => x.UrlHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
                entity.Property(x => x.RoundKey).HasMaxLength(120);
                entity.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(64);
                entity.Property(x => x.Decision).HasMaxLength(400);
                entity.HasIndex(x => new { x.Host, x.RequestedAtUtc }).HasDatabaseName("IX_OfficialSourceFetches_Host_Time");
                entity.HasIndex(x => new { x.RoundKey, x.UrlHash }).HasDatabaseName("IX_OfficialSourceFetches_Round_Url");
                entity.HasIndex(x => new { x.MatchId, x.Purpose }).HasDatabaseName("IX_OfficialSourceFetches_Match_Purpose");
            });

            modelBuilder.Entity<OfficialSourceCacheEntry>(entity =>
            {
                entity.ToTable("OfficialSourceCache");
                entity.HasKey(x => x.UrlHash);
                entity.Property(x => x.UrlHash).HasMaxLength(64);
                entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.ETag).HasMaxLength(200);
                entity.Property(x => x.LastModified).HasMaxLength(64);
                entity.Property(x => x.ContentType).HasMaxLength(100);
                entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ProcessedHash).HasMaxLength(64);
            });

            modelBuilder.Entity<OfficialMatchLink>(entity =>
            {
                entity.ToTable("OfficialMatchLinks");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.OfficialMatchId).HasMaxLength(200).IsRequired();
                entity.Property(x => x.OfficialUrl).HasMaxLength(500);
                entity.Property(x => x.OfficialHomeName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.OfficialAwayName).HasMaxLength(200).IsRequired();
                entity.HasIndex(x => new { x.SourceKey, x.OfficialMatchId }).IsUnique()
                      .HasDatabaseName("UX_OfficialMatchLinks_Source_OfficialMatch");
                entity.HasIndex(x => new { x.MatchId, x.SourceKey }).IsUnique()
                      .HasDatabaseName("UX_OfficialMatchLinks_Match_Source");
            });

            // ── TAKIM SAĞLAYICI KİMLİĞİ (resmî UEFA fikstür kaynağı için; additive) ──
            // Kanonik takım ↔ sağlayıcı takım kimliği. Aynı sağlayıcı kimliği iki kanonik takıma
            // bağlanamaz (tekil indeks): yanlış takıma fikstür yazmanın önündeki son kapı.
            modelBuilder.Entity<TeamProviderIdentity>(entity =>
            {
                entity.ToTable("TeamProviderIdentities");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Provider).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ProviderTeamId).HasMaxLength(80).IsRequired();
                entity.Property(x => x.ProviderTeamName).HasMaxLength(200);
                entity.Property(x => x.MatchedBy).HasMaxLength(40).IsRequired();
                entity.HasIndex(x => new { x.Provider, x.ProviderTeamId }).IsUnique()
                      .HasDatabaseName("UX_TeamProviderIdentities_Provider_ProviderTeam");
                entity.HasIndex(x => new { x.Provider, x.TeamId })
                      .HasDatabaseName("IX_TeamProviderIdentities_Provider_Team");
            });

            // ── KRİTİK GELİŞME (resmî yapılandırılmış veri; aynı kanıt tek satır) ──
            modelBuilder.Entity<MatchCriticalDevelopment>(entity =>
            {
                entity.ToTable("MatchCriticalDevelopments");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.OfficialUrl).HasMaxLength(500);
                entity.Property(x => x.DevelopmentType).HasMaxLength(32).IsRequired();
                entity.Property(x => x.AffectedPlayerId).HasMaxLength(120);
                entity.Property(x => x.Severity).HasMaxLength(16).IsRequired();
                entity.Property(x => x.VerificationStatus).HasMaxLength(32).IsRequired();
                entity.Property(x => x.EvidenceHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.PreviousValue).HasMaxLength(200);
                entity.Property(x => x.NewValue).HasMaxLength(200);
                entity.Property(x => x.SummaryTr).HasMaxLength(400).IsRequired();
                entity.Property(x => x.NotificationNote).HasMaxLength(200);
                entity.HasIndex(x => new { x.MatchId, x.EvidenceHash }).IsUnique()
                      .HasDatabaseName("UX_MatchCriticalDevelopments_Match_Evidence");
            });
            // ── BİTMİŞ MAÇ ANALİZ METNİ (arka planda, doğrulanmış veriden; maç başına tek satır) ──
            modelBuilder.Entity<MatchPostMatchSummary>(entity =>
            {
                entity.ToTable("MatchPostMatchSummaries");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.InputHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Text).HasMaxLength(1200).IsRequired();
                entity.Property(x => x.Generator).HasMaxLength(32).IsRequired();
                entity.HasIndex(x => x.MatchId).IsUnique()
                      .HasDatabaseName("UX_MatchPostMatchSummaries_Match");
            });
            // ── RESMÎ VİDEO KAYNAK KATALOĞU + KALICI KEŞİF KUYRUĞU/DEFTERİ ──
            modelBuilder.Entity<OfficialVideoSourceRecord>(entity =>
            {
                entity.ToTable("OfficialVideoSourceCatalog");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Key).HasMaxLength(80).IsRequired();
                entity.Property(x => x.Publisher).HasMaxLength(160).IsRequired();
                entity.Property(x => x.Platform).HasMaxLength(16).IsRequired();
                entity.Property(x => x.YouTubeChannelId).HasMaxLength(64);
                entity.Property(x => x.FeedUrl).HasMaxLength(400);
                entity.Property(x => x.ClubName).HasMaxLength(160);
                entity.Property(x => x.LeagueIds).HasMaxLength(100);
                entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
                entity.Property(x => x.DiscoveredVia).HasMaxLength(64).IsRequired();
                entity.Property(x => x.WikidataId).HasMaxLength(32);
                entity.Property(x => x.OfficialWebsite).HasMaxLength(300);
                entity.Property(x => x.VerificationEvidence).HasMaxLength(1000).IsRequired();
                entity.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UX_OfficialVideoSourceCatalog_Key");
                entity.HasIndex(x => x.YouTubeChannelId).HasDatabaseName("IX_OfficialVideoSourceCatalog_Channel");
                entity.Property(x => x.Domain).HasMaxLength(200);
                entity.Property(x => x.SourceKind).HasMaxLength(24);
                entity.Property(x => x.Country).HasMaxLength(8);
                entity.Property(x => x.WebsiteStatus).HasMaxLength(24);
                entity.Property(x => x.WebsiteEvidence).HasMaxLength(1000);
                entity.Property(x => x.RobotsStatus).HasMaxLength(24);
                entity.Property(x => x.LastError).HasMaxLength(400);
                entity.Property(x => x.CircuitState).HasMaxLength(12).IsRequired().HasDefaultValue("Closed");
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.Property(x => x.SiteYouTubeHandles).HasMaxLength(600);
                entity.HasIndex(x => x.Domain).HasDatabaseName("IX_OfficialVideoSourceCatalog_Domain");
            });
            modelBuilder.Entity<VideoDiscoveryCursor>(entity =>
            {
                entity.ToTable("VideoDiscoveryCursors");
                entity.HasKey(x => x.Name);
                entity.Property(x => x.Name).HasMaxLength(64);
            });
            modelBuilder.Entity<OfficialWebFeed>(entity =>
            {
                entity.ToTable("OfficialWebFeeds");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.Url).HasMaxLength(450).IsRequired();
                entity.Property(x => x.Kind).HasMaxLength(24).IsRequired();
                entity.Property(x => x.DiscoveredVia).HasMaxLength(40).IsRequired();
                entity.Property(x => x.RobotsStatus).HasMaxLength(24);
                entity.Property(x => x.LastError).HasMaxLength(400);
                entity.HasIndex(x => x.Url).IsUnique().HasDatabaseName("UX_OfficialWebFeeds_Url");
                entity.HasIndex(x => new { x.IsActive, x.NextFetchUtc }).HasDatabaseName("IX_OfficialWebFeeds_Due");
            });
            modelBuilder.Entity<OfficialWebVideoEntry>(entity =>
            {
                entity.ToTable("OfficialWebVideoEntries");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.PageUrl).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.PageUrlHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
                entity.Property(x => x.FoldedTitle).HasMaxLength(400).IsRequired();
                entity.Property(x => x.DatePrecision).HasMaxLength(12).IsRequired();
                entity.Property(x => x.YouTubeVideoId).HasMaxLength(16);
                entity.Property(x => x.VideoIdEvidence).HasMaxLength(24);
                entity.Property(x => x.JsonLdName).HasMaxLength(400);
                entity.Property(x => x.PageOutcome).HasMaxLength(24);
                entity.HasIndex(x => x.PageUrlHash).IsUnique().HasDatabaseName("UX_OfficialWebVideoEntries_Page");
                entity.HasIndex(x => new { x.SourceKey, x.PublishedUtc }).HasDatabaseName("IX_OfficialWebVideoEntries_SourceDate");
                entity.HasIndex(x => x.YouTubeVideoId).HasDatabaseName("IX_OfficialWebVideoEntries_YouTube");
            });
            modelBuilder.Entity<MatchResultObservation>(entity =>
            {
                entity.ToTable("MatchResultObservations");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.OfficialStatus).HasMaxLength(32).IsRequired();
                entity.Property(x => x.ExistingStatus).HasMaxLength(32);
                entity.Property(x => x.ExistingSource).HasMaxLength(120);
                entity.Property(x => x.Decision).HasMaxLength(40).IsRequired();
                entity.Property(x => x.SourceMatchId).HasMaxLength(80);
                entity.Property(x => x.SourceUrl).HasMaxLength(400);
                entity.Property(x => x.SourceHomeName).HasMaxLength(120);
                entity.Property(x => x.SourceAwayName).HasMaxLength(120);
                entity.Property(x => x.OfficialResultDetail).HasMaxLength(8);
                entity.Property(x => x.ValidationResult).HasMaxLength(40);
                entity.Property(x => x.ConflictStatus).HasMaxLength(16);
                entity.Property(x => x.ParserVersion).HasMaxLength(32);
                entity.Property(x => x.ContentHash).HasMaxLength(64);
                entity.HasIndex(x => new { x.MatchId, x.ObservedAtUtc }).HasDatabaseName("IX_MatchResultObservations_Match");
            });
            modelBuilder.Entity<OfficialDataSource>(entity =>
            {
                entity.ToTable("OfficialDataSources");
                entity.HasKey(x => x.SourceId);
                entity.Property(x => x.SourceId).HasMaxLength(80);
                entity.Property(x => x.SourceName).HasMaxLength(120).IsRequired();
                entity.Property(x => x.OfficialDomain).HasMaxLength(200).IsRequired();
                entity.Property(x => x.OrganizationIds).HasMaxLength(120).IsRequired();
                entity.Property(x => x.SourceType).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ContentKind).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Capabilities).HasMaxLength(200).IsRequired();
                entity.Property(x => x.VerificationEvidence).HasMaxLength(1200).IsRequired();
                entity.Property(x => x.RegistryStatus).HasMaxLength(32).IsRequired();
                entity.Property(x => x.RobotsStatus).HasMaxLength(24).IsRequired();
                entity.Property(x => x.ParserVersion).HasMaxLength(32).IsRequired();
                entity.Property(x => x.LastError).HasMaxLength(400);
            });
            modelBuilder.Entity<MatchResultCheck>(entity =>
            {
                entity.ToTable("MatchResultChecks");
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.State).HasMaxLength(24).IsRequired();
                entity.Property(x => x.LastOutcome).HasMaxLength(120);
                entity.Property(x => x.LastSourceKey).HasMaxLength(80);
                entity.Property(x => x.LastErrorClass).HasMaxLength(32);
                entity.Property(x => x.LastValidationStatus).HasMaxLength(48);
                entity.Property(x => x.ResolvedStatus).HasMaxLength(16);
                entity.Property(x => x.LockOwner).HasMaxLength(64);
                entity.HasIndex(x => new { x.State, x.NextCheckUtc }).HasDatabaseName("IX_MatchResultChecks_Due");
            });
            modelBuilder.Entity<MatchStatisticsCheck>(entity =>
            {
                entity.ToTable("MatchStatisticsChecks");
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.State).HasMaxLength(24).IsRequired();
                entity.Property(x => x.Completeness).HasMaxLength(16).IsRequired();
                entity.Property(x => x.LastOutcome).HasMaxLength(160);
                entity.Property(x => x.LastSourceKey).HasMaxLength(80);
                entity.Property(x => x.LockOwner).HasMaxLength(64);
                entity.HasIndex(x => new { x.State, x.NextCheckUtc }).HasDatabaseName("IX_MatchStatisticsChecks_Due");
            });
            modelBuilder.Entity<MatchStatisticObservation>(entity =>
            {
                entity.ToTable("MatchStatisticObservations");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceKey).HasMaxLength(80).IsRequired();
                entity.Property(x => x.SourceUrl).HasMaxLength(400);
                entity.Property(x => x.Side).HasMaxLength(8).IsRequired();
                entity.Property(x => x.FieldsJson).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.ParserVersion).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Decision).HasMaxLength(32).IsRequired();
                entity.Property(x => x.ConflictDetail).HasMaxLength(400);
                entity.HasIndex(x => new { x.MatchId, x.SourceKey, x.Side, x.ContentHash }).HasDatabaseName("IX_MatchStatisticObservations_Dedupe");
            });
            modelBuilder.Entity<MatchPredictionSnapshot>(entity =>
            {
                entity.ToTable("MatchPredictionSnapshots");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SnapshotId).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
                entity.Property(x => x.CalibrationRunId).HasMaxLength(40);
                entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
                entity.Property(x => x.PayloadJson).IsRequired();
                entity.Property(x => x.InputHash).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => x.SnapshotId).IsUnique().HasDatabaseName("UX_MatchPredictionSnapshots_SnapshotId");
                entity.HasIndex(x => new { x.MatchId, x.IsCurrent }).HasDatabaseName("IX_MatchPredictionSnapshots_Current");
                entity.Property(x => x.PredictionEligibility).HasMaxLength(16);
                entity.Property(x => x.PublicationStatus).HasMaxLength(16);
                entity.Property(x => x.PreviousSnapshotId).HasMaxLength(40);
                entity.Property(x => x.TriggerType).HasMaxLength(32);
                entity.Property(x => x.TriggerSource).HasMaxLength(80);
                entity.Property(x => x.IntelligenceFingerprint).HasMaxLength(64);
                entity.Property(x => x.SelectionVersion).HasMaxLength(24);
            });
            modelBuilder.Entity<LeaguePredictionEligibility>(entity =>
            {
                entity.ToTable("LeaguePredictionEligibilities");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.RunId).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
                entity.Property(x => x.PolicyVersion).HasMaxLength(24).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
                entity.Property(x => x.ReasonsJson).IsRequired();
                entity.Property(x => x.MetricsJson).IsRequired();
                entity.HasIndex(x => new { x.RunId, x.LeagueId }).IsUnique().HasDatabaseName("UX_LeaguePredictionEligibilities_Run_League");
            });
            modelBuilder.Entity<PredictionRecomputeRequest>(entity =>
            {
                entity.ToTable("PredictionRecomputeRequests");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.TriggerType).HasMaxLength(32).IsRequired();
                entity.Property(x => x.TriggerSource).HasMaxLength(80).IsRequired();
                entity.Property(x => x.DedupeKey).HasMaxLength(160).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
                entity.Property(x => x.ResultSnapshotId).HasMaxLength(40);
                entity.Property(x => x.Outcome).HasMaxLength(200);
                entity.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("UX_PredictionRecomputeRequests_DedupeKey");
                entity.HasIndex(x => new { x.Status, x.DueAtUtc }).HasDatabaseName("IX_PredictionRecomputeRequests_Due");
            });
            modelBuilder.Entity<PredictionScorecard>(entity =>
            {
                entity.ToTable("PredictionScorecards");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SnapshotId).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Eligibility).HasMaxLength(16).IsRequired();
                entity.Property(x => x.FinalStatus).HasMaxLength(24);
                entity.HasIndex(x => x.MatchId).IsUnique().HasDatabaseName("UX_PredictionScorecards_MatchId");
            });
            modelBuilder.Entity<PredictionDiagnostic>(entity =>
            {
                entity.ToTable("PredictionDiagnostics");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SnapshotId).HasMaxLength(40);
                entity.Property(x => x.Kind).HasMaxLength(48).IsRequired();
                entity.Property(x => x.Detail).HasMaxLength(2000).IsRequired();
                entity.Property(x => x.DedupeKey).HasMaxLength(160).IsRequired();
                entity.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("UX_PredictionDiagnostics_DedupeKey");
                entity.HasIndex(x => new { x.Kind, x.CreatedAtUtc }).HasDatabaseName("IX_PredictionDiagnostics_Kind");
            });
            modelBuilder.Entity<PredictionModelRun>(entity =>
            {
                entity.ToTable("PredictionModelRuns");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.RunId).HasMaxLength(40).IsRequired();
                entity.Property(x => x.ModelVersion).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
                entity.Property(x => x.ParametersJson).IsRequired();
                entity.Property(x => x.MetricsJson).IsRequired();
                entity.HasIndex(x => x.RunId).IsUnique().HasDatabaseName("UX_PredictionModelRuns_RunId");
            });
            modelBuilder.Entity<MatchVideoDiscoveryQueueItem>(entity =>
            {
                entity.ToTable("MatchVideoDiscoveryQueue");
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.State).HasMaxLength(32).IsRequired();
                entity.Property(x => x.LastOutcome).HasMaxLength(64);
                entity.Property(x => x.LastError).HasMaxLength(400);
                entity.Property(x => x.LockOwner).HasMaxLength(64);
                entity.Property(x => x.EnqueueReason).HasMaxLength(24).IsRequired();
                entity.Property(x => x.RequeueReason).HasMaxLength(64);
                entity.HasIndex(x => new { x.NextAttemptUtc, x.EndUtc }).HasDatabaseName("IX_MatchVideoDiscoveryQueue_Due");
            });
            modelBuilder.Entity<MatchVideoDiscoveryAttempt>(entity =>
            {
                entity.ToTable("MatchVideoDiscoveryAttempts");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ExternalFixtureId).HasMaxLength(64).IsRequired();
                entity.Property(x => x.RowKind).HasMaxLength(16).IsRequired();
                entity.Property(x => x.SourceKey).HasMaxLength(80);
                entity.Property(x => x.SourceKind).HasMaxLength(24);
                entity.Property(x => x.SourceChannelOrDomain).HasMaxLength(200);
                entity.Property(x => x.SearchExpression).HasMaxLength(300);
                entity.Property(x => x.ErrorType).HasMaxLength(64);
                entity.Property(x => x.CandidateUrl).HasMaxLength(600);
                entity.Property(x => x.CandidateTitle).HasMaxLength(300);
                entity.Property(x => x.VideoType).HasMaxLength(32);
                entity.Property(x => x.VerificationStatus).HasMaxLength(32);
                entity.Property(x => x.EmbedResult).HasMaxLength(200);
                entity.Property(x => x.Evidence).HasMaxLength(600);
                entity.Property(x => x.RejectionReason).HasMaxLength(300);
                entity.HasIndex(x => new { x.MatchId, x.AttemptedAtUtc }).HasDatabaseName("IX_MatchVideoDiscoveryAttempts_Match");
            });
            modelBuilder.Entity<OfficialMatchLink>().Property(x => x.OfficialVenue).HasMaxLength(200);
            modelBuilder.Entity<OfficialMatchLink>().Property(x => x.OfficialStatus).HasMaxLength(32);
            modelBuilder.Entity<Match>().Property(x => x.ScheduleSource).HasMaxLength(80);
            modelBuilder.Entity<Match>().Property(x => x.ResultVerificationStatus).HasMaxLength(32);
            modelBuilder.Entity<Match>().Property(x => x.ResultDetail).HasMaxLength(8);

            // ── AI MAÇ ANALİZİ (arka planda üretilmiş, maç başına tek satır) ──
            modelBuilder.Entity<MatchAnalysisSnapshot>(entity =>
            {
                entity.ToTable("MatchAnalysisSnapshots");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.MatchId).IsUnique().HasDatabaseName("UX_MatchAnalysisSnapshots_Match");
                entity.HasIndex(x => x.GeneratedAtUtc).HasDatabaseName("IX_MatchAnalysisSnapshots_Generated");
                entity.Property(x => x.InputHash).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
                entity.Property(x => x.Generator).HasMaxLength(32).IsRequired();
                entity.Property(x => x.ContentJson).IsRequired();
                entity.Property(x => x.EvidenceJson).IsRequired();
                entity.Property(x => x.FlatText).IsRequired();
            });

            // ── BİLDİRİM SÖZLEŞMESİ (additive) ──────────────────────────────
            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.Property(x => x.NotificationType).HasMaxLength(64);
                entity.Property(x => x.Route).HasMaxLength(200);
                entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
                // DB seviyesinde tekillik: aynı anahtar ikinci kez YAZILAMAZ (legacy satırlar null).
                entity.HasIndex(x => x.IdempotencyKey).IsUnique()
                      .HasFilter("[IdempotencyKey] IS NOT NULL")
                      .HasDatabaseName("UX_UserNotifications_IdempotencyKey");
            });

            modelBuilder.Entity<UserNotificationPreference>(entity =>
            {
                entity.ToTable("UserNotificationPreferences");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.PrefKey).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => new { x.UserId, x.PrefKey }).IsUnique()
                      .HasDatabaseName("UX_UserNotificationPreferences_User_Key");
            });

            // MatchLineupPlayer — Guid PK, index on MatchId for fast reads
            modelBuilder.Entity<MatchLineupPlayer>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Side).HasMaxLength(10).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(10).IsRequired();
                entity.Property(x => x.Position).HasMaxLength(5);
                entity.Property(x => x.PlayerName).HasMaxLength(120).IsRequired();
                entity.HasIndex(x => x.MatchId);
            });

            // MatchPlayerStatus — Guid PK, index on MatchId
            modelBuilder.Entity<MatchPlayerStatus>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.PlayerName).HasMaxLength(120).IsRequired();
                entity.Property(x => x.Reason).HasMaxLength(256);
                entity.HasIndex(x => x.MatchId);
            });

            // ── Sprint 3: Live match intelligence ────────────────────────────

            // MatchLiveStats — MatchId is PK (one row per match)
            modelBuilder.Entity<MatchLiveStats>(entity =>
            {
                entity.HasKey(x => x.MatchId);
                entity.Property(x => x.MatchId).ValueGeneratedNever();
                entity.Property(x => x.Phase).HasMaxLength(10);
            });

            // MatchMomentumSnapshot — Guid PK, compound index for fast per-match queries
            modelBuilder.Entity<MatchMomentumSnapshot>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.MatchId, x.MinuteBucket });
            });

            // MatchLiveEvent — Detail column (Sprint 3 extension of existing entity)
            modelBuilder.Entity<MatchLiveEvent>(entity =>
            {
                entity.Property(x => x.Detail).HasMaxLength(200);
            });

            // ── Sprint 3b: Distributed ingestion lock ────────────────────────
            // Singleton row — Id is always 1, never auto-generated.
            modelBuilder.Entity<LiveIngestionLock>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedNever();
                entity.Property(x => x.OwnerInstanceId).HasMaxLength(200).IsRequired();
            });

            // ── Sprint 0: Fixture sync distributed lock ───────────────────────
            // Singleton row — Id is always 1, never auto-generated.
            modelBuilder.Entity<FixtureSyncLock>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedNever();
                entity.Property(x => x.OwnerInstanceId).HasMaxLength(200).IsRequired();
            });

            // ── Sprint 4: NABIZ feed intelligence ────────────────────────────
            modelBuilder.Entity<MatchSocialFeedItem>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Source).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Author).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Headline).HasMaxLength(500).IsRequired();
                entity.Property(x => x.Summary).HasMaxLength(600);
                entity.Property(x => x.ImageUrl).HasMaxLength(1000);
                entity.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();

                // ContentHash must be globally unique — deduplication guard
                entity.HasIndex(x => x.ContentHash).IsUnique();

                // Fast per-match feed query
                entity.HasIndex(x => new { x.MatchId, x.PublishedAt });
            });
        }
    }
}