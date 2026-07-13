using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Formax.Infrastructure.Configurations;

/// <summary>
/// Radar Source Engine (R.8.5) — EF mapping for the staging table. Applied
/// explicitly in FormaxDbContext.OnModelCreating (this codebase configures inline,
/// so configuration classes must be wired with ApplyConfiguration to take effect).
/// </summary>
public sealed class StagedSourceItemConfiguration : IEntityTypeConfiguration<StagedSourceItem>
{
    public void Configure(EntityTypeBuilder<StagedSourceItem> builder)
    {
        builder.ToTable("StagedSourceItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RawId).HasMaxLength(256);
        builder.Property(x => x.EntityType).HasMaxLength(64);
        builder.Property(x => x.RawName).HasMaxLength(256);
        builder.Property(x => x.CanonicalName).HasMaxLength(256);

        builder.Property(x => x.Category).HasConversion<int>();

        // Query helpers for the future Intelligence consumer.
        builder.HasIndex(x => new { x.Processed, x.Category });
        builder.HasIndex(x => x.SourceId);
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
