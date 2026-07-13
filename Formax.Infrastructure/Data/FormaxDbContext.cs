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

        // 🌐 FORMAX Canonical Domain — GDP tarafından beslenen eksik entity'ler (Domain tamamlama)
        public DbSet<Player> Players { get; set; } = null!;
        public DbSet<Competition> Competitions { get; set; } = null!;
        public DbSet<CompetitionStanding> CompetitionStandings { get; set; } = null!;
        public DbSet<Coach> Coaches { get; set; } = null!;
        public DbSet<Referee> Referees { get; set; } = null!;
        public DbSet<Venue> Venues { get; set; } = null!;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
                entity.HasOne(x => x.Competition)
                      .WithMany()
                      .HasForeignKey(x => x.CompetitionId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Venue)
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
            });

            // 🔥 KRİTİK FIX (WithMany → navigation bağlandı)
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
        }
    }
}