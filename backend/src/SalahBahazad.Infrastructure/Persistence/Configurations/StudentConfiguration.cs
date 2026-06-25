using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FirebaseUid).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.FirebaseUid }).IsUnique();

        builder.Property(s => s.Serial).HasMaxLength(20).IsRequired();

        builder.Property(s => s.FullName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(s => s.ParentPhonePrimary).HasMaxLength(32).IsRequired();
        builder.Property(s => s.ParentPhoneSecondary).HasMaxLength(32);
        builder.Property(s => s.SchoolName).HasMaxLength(200).IsRequired();

        // R2 object key only — never bytes or a durable URL (FR-PLAT-AST-004).
        builder.Property(s => s.IdImageObjectKey).HasMaxLength(512);

        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.RejectionReason).HasMaxLength(1000);

        builder.Property(s => s.TermsVersion).HasMaxLength(50);
        builder.Property(s => s.TermsAcceptedAtUtc);
        builder.Property(s => s.LastSeenAtUtc);

        // Soft-delete columns
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);
        builder.Property(s => s.DeletedAtUtc);
        builder.Property(s => s.DeletedById);

        // Grade is tenant taxonomy; restrict delete so a grade in use can't be removed (FR-PLAT-TAX-004).
        builder.HasOne<Grade>()
            .WithMany()
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        // City/Region are global seeded reference data (FR-PLAT-TAX-003) — restrict delete.
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(s => s.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Region>()
            .WithMany()
            .HasForeignKey(s => s.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serials are unique per tenant (FR-APP-VID-003). Includes soft-deleted rows so a serial is never
        // reissued; the registration handler seeds from this set with IgnoreQueryFilters.
        builder.HasIndex(s => new { s.TenantId, s.Serial }).IsUnique();

        // Primary triage index: list/filter by status within a tenant (FR-ADM-STU-001).
        builder.HasIndex(s => new { s.TenantId, s.Status });

        // FK lookup indexes.
        builder.HasIndex(s => s.GradeId);
        builder.HasIndex(s => s.CityId);
        builder.HasIndex(s => s.RegionId);
    }
}
