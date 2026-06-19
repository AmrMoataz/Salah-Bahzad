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

    /// <summary>
    /// Runs <paramref name="action"/> inside a single database transaction, committing on success.
    /// Buffered domain events are dispatched only after the commit. Used by the transaction pipeline
    /// behaviour for <see cref="ITransactionalRequest"/> commands.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> action, CancellationToken cancellationToken = default);
}
