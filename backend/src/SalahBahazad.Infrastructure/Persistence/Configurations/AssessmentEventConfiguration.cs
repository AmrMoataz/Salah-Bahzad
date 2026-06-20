using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentEventConfiguration : IEntityTypeConfiguration<AssessmentEvent>
{
    public void Configure(EntityTypeBuilder<AssessmentEvent> builder)
    {
        builder.ToTable("assessment_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.UserAssignmentId).IsRequired();
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.QuestionOrder);
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.DurationMs);

        builder.HasOne<UserAssignment>()
            .WithMany()
            .HasForeignKey(e => e.UserAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // High-volume timeline read in OccurredAt order per assignment (FR-ADM-REV-003).
        builder.HasIndex(e => new { e.TenantId, e.UserAssignmentId, e.OccurredAtUtc });
    }
}
