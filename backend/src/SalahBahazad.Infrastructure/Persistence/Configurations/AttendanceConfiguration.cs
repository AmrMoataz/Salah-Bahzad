using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("attendance");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.StudentId).IsRequired();
        builder.Property(a => a.SessionId).IsRequired();
        builder.Property(a => a.EnrollmentId).IsRequired();

        // Phase 5 grading writes these.
        builder.Property(a => a.AssignmentScore);
        builder.Property(a => a.BestQuizPercent);
        builder.Property(a => a.VideosWatched).IsRequired();

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // One attendance record per student+session within a tenant (FR-PLAT-ATT-001).
        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.SessionId }).IsUnique();
    }
}
