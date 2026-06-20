using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="EnrollmentVideoAccess"/> rows. The relationship to <see cref="Enrollment"/> is configured
/// on the aggregate root; here only the scalar columns and lookup index are defined.
/// </summary>
internal sealed class EnrollmentVideoAccessConfiguration : IEntityTypeConfiguration<EnrollmentVideoAccess>
{
    public void Configure(EntityTypeBuilder<EnrollmentVideoAccess> builder)
    {
        builder.ToTable("enrollment_video_access");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EnrollmentId).IsRequired();
        builder.Property(a => a.VideoId).IsRequired();
        builder.Property(a => a.AccessAllowed).IsRequired();
        builder.Property(a => a.AccessRemaining).IsRequired();

        builder.HasIndex(a => new { a.EnrollmentId, a.VideoId });
    }
}
