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

        // Nullable subjects — exactly one is set: an assignment event or a quiz-attempt event (enforced in code).
        builder.Property(e => e.UserAssignmentId);
        builder.Property(e => e.QuizAttemptId);
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.QuestionOrder);
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.DurationMs);

        // Optional FK to the assignment (null for quiz events). The quiz attempt lives in an owned table with no
        // root DbSet, so QuizAttemptId is an indexed column without a navigational FK (telemetry tolerates it).
        builder.HasOne<UserAssignment>()
            .WithMany()
            .HasForeignKey(e => e.UserAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // High-volume timeline reads in OccurredAt order per subject (FR-ADM-REV-003, FR-PLAT-QZ-006).
        builder.HasIndex(e => new { e.TenantId, e.UserAssignmentId, e.OccurredAtUtc });
        builder.HasIndex(e => new { e.TenantId, e.QuizAttemptId, e.OccurredAtUtc });
    }
}
