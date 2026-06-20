using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.StudentId).IsRequired();
        builder.Property(e => e.SessionId).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Method).IsRequired();
        builder.Property(e => e.CodeId);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.EnrolledAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc);

        // Soft-delete columns
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.DeletedAtUtc);
        builder.Property(e => e.DeletedById);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Per-video access counters, managed through the root (field-backed navigation).
        builder.HasMany(e => e.VideoAccesses)
            .WithOne()
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.VideoAccesses).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Append-only payment trail, managed through the root (field-backed navigation).
        builder.HasMany(e => e.Payments)
            .WithOne()
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.TenantId, e.SessionId, e.Status });

        // At most one ACTIVE enrollment per (tenant, student, session) — FR-PLAT-ENR-006. Filtered on the
        // Active status value (EnrollmentStatus.Active = 0); non-active rows are unconstrained for history.
        builder.HasIndex(e => new { e.TenantId, e.StudentId, e.SessionId })
            .IsUnique()
            .HasFilter("\"Status\" = 0");
    }
}
