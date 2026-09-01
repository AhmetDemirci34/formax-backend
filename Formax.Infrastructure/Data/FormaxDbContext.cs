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

            modelBuilder.Entity<UserPick>()
                .HasIndex(x => new { x.UserId, x.MatchId })
                .IsUnique();

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