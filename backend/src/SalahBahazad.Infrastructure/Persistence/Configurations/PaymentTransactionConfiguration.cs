using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PaymentTransaction"/> rows (the money trail). The relationship to <see cref="Enrollment"/>
/// is configured on the aggregate root; here only the scalar columns and lookup index are defined.
/// </summary>
internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.EnrollmentId).IsRequired();
        builder.Property(p => p.Method).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.CodeId);
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.ProviderRef).HasMaxLength(200);

        builder.HasIndex(p => p.EnrollmentId);
    }
}
