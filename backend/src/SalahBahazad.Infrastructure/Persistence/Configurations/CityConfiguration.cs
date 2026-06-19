using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Infrastructure.Persistence.SeedData;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.NameEn).HasMaxLength(120).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(120).IsRequired();

        builder.HasIndex(c => c.NameEn);

        // Global, read-only Egypt reference data (FR-PLAT-TAX-003) — seeded via migration.
        builder.HasData(ReferenceData.Cities);
    }
}
