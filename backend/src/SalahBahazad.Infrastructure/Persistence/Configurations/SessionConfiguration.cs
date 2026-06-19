using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(4000);
        builder.Property(s => s.Price).HasPrecision(18, 2);
        builder.Property(s => s.ValidityDays).IsRequired();

        // R2 object key only — never bytes or a durable URL (FR-PLAT-AST-004).
        builder.Property(s => s.ThumbnailObjectKey).HasMaxLength(512);

        builder.Property(s => s.Status).IsRequired();

        // Soft-delete columns
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);
        builder.Property(s => s.DeletedAtUtc);
        builder.Property(s => s.DeletedById);

        // Gating-quiz configuration owned 1:1 (FR-PLAT-SES-006).
        builder.OwnsOne(s => s.QuizSetting, QuizSettingConfiguration.Configure);
        builder.Navigation(s => s.QuizSetting).IsRequired(false);

        // Grade & specialization are tenant taxonomy; restrict delete so one in use can't be removed
        // (FR-PLAT-TAX-004) — also enforced in the handlers.
        builder.HasOne<Grade>()
            .WithMany()
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Specialization>()
            .WithMany()
            .HasForeignKey(s => s.SpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional self-referencing prerequisite (FR-PLAT-SES-004); restrict so a prerequisite cannot be
        // hard-deleted out from under dependents (sessions soft-delete anyway).
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(s => s.PrerequisiteSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ordered child videos & materials, managed through the aggregate root (field-backed navigations).
        builder.HasMany(s => s.Videos)
            .WithOne()
            .HasForeignKey(v => v.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Videos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Materials)
            .WithOne()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Materials).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Catalogue filter indexes (FR-ADM-SES-001): by grade and by status within a tenant.
        builder.HasIndex(s => new { s.TenantId, s.GradeId });
        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => s.SpecializationId);
    }
}
