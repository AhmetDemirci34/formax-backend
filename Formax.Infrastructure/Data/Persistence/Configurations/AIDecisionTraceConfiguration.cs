using Formax.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Formax.Infrastructure.Configurations
{
    public class AIDecisionTraceConfiguration
        : IEntityTypeConfiguration<AIDecisionTrace>
    {
        public void Configure(EntityTypeBuilder<AIDecisionTrace> builder)
        {
            builder.ToTable("AIDecisionTraces");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.GuardrailDecision)
                   .IsRequired()
                   .HasMaxLength(32);

            builder.Property(x => x.AiBehaviorState)
                   .IsRequired()
                   .HasMaxLength(32);

            builder.Property(x => x.CreatedAt)
                   .IsRequired();
        }
    }
}
