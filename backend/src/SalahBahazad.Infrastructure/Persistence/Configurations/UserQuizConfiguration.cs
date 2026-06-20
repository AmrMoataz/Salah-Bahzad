using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class UserQuizConfiguration : IEntityTypeConfiguration<UserQuiz>
{
    public void Configure(EntityTypeBuilder<UserQuiz> builder)
    {
        builder.ToTable("user_quizzes");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.EnrollmentId).IsRequired();
        builder.Property(q => q.StudentId).IsRequired();
        builder.Property(q => q.GatedSessionId).IsRequired();
        builder.Property(q => q.SourceSessionId).IsRequired();

        // Snapshot of the prerequisite's QuizSetting (immune to later edits, FR-PLAT-SES-007).
        builder.Property(q => q.TimeLimitMinutes).IsRequired();
        builder.Property(q => q.QuestionCount).IsRequired();
        builder.Property(q => q.AttemptCount).IsRequired();
        builder.Property(q => q.MinPassPercent).IsRequired();

        builder.Property(q => q.AttemptsUsed).IsRequired();
        builder.Property(q => q.BestPercent);
        builder.Property(q => q.Passed).IsRequired();

        // Computed convenience reference into the owned attempts — not a stored relationship.
        builder.Ignore(q => q.ActiveAttempt);

        // Referential integrity; all restrict (enrollment/session/student soft-delete, so never orphaned).
        builder.HasOne<Enrollment>()
            .WithMany()
            .HasForeignKey(q => q.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(q => q.GatedSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(q => q.SourceSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(q => q.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Each attempt is an immutable snapshot of the questions drawn for that sitting (FR-PLAT-SES-007). The
        // student's selected option is the only mutable column. Questions → options nest as their own tables.
        builder.OwnsMany(q => q.Attempts, attempt =>
        {
            attempt.ToTable("quiz_attempts");
            attempt.WithOwner().HasForeignKey("UserQuizId");
            attempt.HasKey(a => a.Id);
            attempt.Property(a => a.Id).ValueGeneratedNever();
            attempt.Property(a => a.Number).IsRequired();
            attempt.Property(a => a.Status).IsRequired();
            attempt.Property(a => a.ScorePercent);
            attempt.Property(a => a.StartedAtUtc).IsRequired();
            attempt.Property(a => a.DeadlineUtc).IsRequired();
            attempt.Property(a => a.SubmittedAtUtc);
            attempt.HasIndex("UserQuizId");

            attempt.OwnsMany(a => a.Questions, question =>
            {
                question.ToTable("quiz_attempt_questions");
                question.WithOwner().HasForeignKey("QuizAttemptId");
                question.HasKey(x => x.Id);
                question.Property(x => x.Id).ValueGeneratedNever();
                question.Property(x => x.QuestionId).IsRequired();
                question.Property(x => x.Order).HasColumnName("DisplayOrder").IsRequired();
                question.Property(x => x.BodyLatex).HasMaxLength(4000);
                question.Property(x => x.ImageObjectKey).HasMaxLength(512);
                question.Property(x => x.Mark).IsRequired();
                question.Property(x => x.SelectedOptionId);
                question.Property(x => x.AnsweredAtUtc);
                question.HasIndex("QuizAttemptId");

                question.OwnsMany(x => x.Options, option =>
                {
                    option.ToTable("quiz_attempt_question_options");
                    option.WithOwner().HasForeignKey("QuizAttemptQuestionId");
                    option.HasKey(o => o.Id);
                    option.Property(o => o.Id).ValueGeneratedNever();
                    option.Property(o => o.Order).HasColumnName("DisplayOrder").IsRequired();
                    option.Property(o => o.Text).HasMaxLength(2000).IsRequired();
                    option.Property(o => o.IsCorrect).IsRequired();
                    option.HasIndex("QuizAttemptQuestionId");
                });
                question.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
            });
            attempt.Navigation(a => a.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(q => q.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Exactly one quiz per enrollment (generation is idempotent, FR-PLAT-ENR-003).
        builder.HasIndex(q => new { q.TenantId, q.EnrollmentId }).IsUnique();

        // Lookups: the student's quiz for a gated session (#1) and the attendance join (FR-PLAT-ATT-002).
        builder.HasIndex(q => new { q.TenantId, q.StudentId, q.GatedSessionId });
        builder.HasIndex(q => q.GatedSessionId);
    }
}
