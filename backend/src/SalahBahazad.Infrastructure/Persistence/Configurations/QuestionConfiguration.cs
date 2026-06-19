using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.SessionId).IsRequired();
        builder.Property(q => q.BodyLatex).HasMaxLength(4000);

        // R2 object key only (FR-PLAT-AST-004).
        builder.Property(q => q.ImageObjectKey).HasMaxLength(512);

        builder.Property(q => q.Mark).IsRequired();
        builder.Property(q => q.IsValidForQuiz).IsRequired();
        builder.Property(q => q.HintUrl).HasMaxLength(1000);

        // Soft-delete columns
        builder.Property(q => q.IsDeleted).HasDefaultValue(false);
        builder.Property(q => q.DeletedAtUtc);
        builder.Property(q => q.DeletedById);

        // Belongs to a session; restrict delete (sessions soft-delete, so questions are never orphaned).
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(q => q.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Owned MCQ options (FR-PLAT-QB-001) — separate table, replaced wholesale by the aggregate.
        builder.OwnsMany(q => q.Options, options =>
        {
            options.ToTable("question_options");
            options.WithOwner().HasForeignKey("QuestionId");
            options.HasKey(o => o.Id);
            options.Property(o => o.Id).ValueGeneratedNever();
            options.Property(o => o.Text).HasMaxLength(2000).IsRequired();
            options.Property(o => o.Order).HasColumnName("DisplayOrder").IsRequired();
            options.Property(o => o.IsCorrect).IsRequired();
        });
        builder.Navigation(q => q.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Variation children (FR-PLAT-QB-003), managed through the aggregate root (field-backed).
        builder.HasMany(q => q.Variations)
            .WithOne()
            .HasForeignKey(v => v.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(q => q.Variations).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Bank is listed/counted per session (FR-ADM-QB-001, FR-ADM-SES-001 stats).
        builder.HasIndex(q => q.SessionId);
        builder.HasIndex(q => new { q.SessionId, q.IsValidForQuiz });
    }
}
