using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Infrastructure.Persistence.SeedData;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.NameEn).HasMaxLength(120).IsRequired();
        builder.Property(r => r.NameAr).HasMaxLength(120).IsRequired();
        builder.Property(r => r.CityId).IsRequired();

        // Region depends on its parent city (FR-PLAT-TAX-003, cascading).
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(r => r.CityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.CityId);

        // Global, read-only Egypt reference data (FR-PLAT-TAX-003) — seeded via migration.
        builder.HasData(ReferenceData.Regions);
    }
}
