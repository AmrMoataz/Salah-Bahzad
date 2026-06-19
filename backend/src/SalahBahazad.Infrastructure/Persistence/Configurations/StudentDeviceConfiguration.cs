using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class StudentDeviceConfiguration : IEntityTypeConfiguration<StudentDevice>
{
    public void Configure(EntityTypeBuilder<StudentDevice> builder)
    {
        builder.ToTable("student_devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.StudentId).IsRequired();
        builder.Property(d => d.DeviceTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(d => d.FingerprintSummary).HasMaxLength(512);
        builder.Property(d => d.BoundAtUtc).IsRequired();
        builder.Property(d => d.IsActive).HasDefaultValue(true);

        builder.Property(d => d.ClearedAtUtc);
        builder.Property(d => d.ClearedById);
        builder.Property(d => d.ClearReason).HasMaxLength(1000);

        // Cleared devices are retained as history; restrict delete (students are soft-deleted, not removed).
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(d => d.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.TenantId, d.StudentId });

        // At most one active device per student (FR-PLAT-DEV-001) — enforced among live bindings only.
        builder.HasIndex(d => d.StudentId)
            .IsUnique()
            .HasFilter("\"IsActive\" = true");
    }
}
