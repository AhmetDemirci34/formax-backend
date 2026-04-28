using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Formax.Infrastructure.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.HomeTeam)
            .WithMany()
            .HasForeignKey(x => x.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AwayTeam)
            .WithMany()
            .HasForeignKey(x => x.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
