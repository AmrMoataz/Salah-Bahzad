using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Application-layer abstraction over the EF DbContext (keeps handlers DB-agnostic).</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Staff> Staff { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
