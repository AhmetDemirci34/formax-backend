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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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