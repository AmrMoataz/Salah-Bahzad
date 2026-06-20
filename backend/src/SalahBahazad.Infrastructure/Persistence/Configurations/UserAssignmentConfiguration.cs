using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class UserAssignmentConfiguration : IEntityTypeConfiguration<UserAssignment>
{
    public void Configure(EntityTypeBuilder<UserAssignment> builder)
    {
        builder.ToTable("user_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EnrollmentId).IsRequired();
        builder.Property(a => a.StudentId).IsRequired();
        builder.Property(a => a.SessionId).IsRequired();
        builder.Property(a => a.Status).IsRequired();

        builder.Property(a => a.ScoreMarks);
        builder.Property(a => a.MaxMarks).IsRequired();
        builder.Property(a => a.CorrectCount);
        builder.Property(a => a.QuestionCount).IsRequired();
        builder.Property(a => a.TimeSpentSeconds).IsRequired();
        builder.Property(a => a.StartedAtUtc).IsRequired();
        builder.Property(a => a.CompletedAtUtc);

        // Referential integrity; all restrict (enrollment/session/student soft-delete, so never orphaned).
        builder.HasOne<Enrollment>()
            .WithMany()
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Immutable snapshot of the bank at generation time (FR-PLAT-SES-007). Owned child table; the student's
        // answer fields are the only mutable columns. Options nest one level deeper as their own table.
        builder.OwnsMany(a => a.Questions, question =>
        {
            question.ToTable("assignment_questions");
            question.WithOwner().HasForeignKey("UserAssignmentId");
            question.HasKey(q => q.Id);
            question.Property(q => q.Id).ValueGeneratedNever();
            question.Property(q => q.QuestionId).IsRequired();
            question.Property(q => q.Order).HasColumnName("DisplayOrder").IsRequired();
            question.Property(q => q.BodyLatex).HasMaxLength(4000);
            question.Property(q => q.ImageObjectKey).HasMaxLength(512);
            question.Property(q => q.Mark).IsRequired();
            question.Property(q => q.HintUrl).HasMaxLength(1000);
            question.Property(q => q.SelectedOptionId);
            question.Property(q => q.AnsweredAtUtc);
            question.HasIndex("UserAssignmentId");

            question.OwnsMany(q => q.Options, option =>
            {
                option.ToTable("assignment_question_options");
                option.WithOwner().HasForeignKey("AssignmentQuestionId");
                option.HasKey(o => o.Id);
                option.Property(o => o.Id).ValueGeneratedNever();
                option.Property(o => o.Order).HasColumnName("DisplayOrder").IsRequired();
                option.Property(o => o.Text).HasMaxLength(2000).IsRequired();
                option.Property(o => o.IsCorrect).IsRequired();
                option.HasIndex("AssignmentQuestionId");
            });
            question.Navigation(q => q.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(a => a.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Exactly one assignment per enrollment (generation is idempotent, FR-PLAT-ENR-003).
        builder.HasIndex(a => new { a.TenantId, a.EnrollmentId }).IsUnique();

        // Lookups: the student's assignment for a session (#1) and the prerequisite gate (FR-PLAT-ENR-007).
        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.SessionId });
        builder.HasIndex(a => new { a.SessionId, a.Status });
    }
}
