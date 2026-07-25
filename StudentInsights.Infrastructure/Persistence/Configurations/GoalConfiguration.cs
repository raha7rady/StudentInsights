using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentInsights.Domain.Entities;

namespace StudentInsights.Infrastructure.Persistence.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(g => g.TargetValue)
            .HasPrecision(10, 2);

        builder.Property(g => g.CurrentValue)
            .HasPrecision(10, 2);

        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(g => g.RelatedActivityId)
            .OnDelete(DeleteBehavior.SetNull);

        // The User relationship is configured once, in UserConfiguration
        // (Cascade). It must NOT be redefined here.
    }
}