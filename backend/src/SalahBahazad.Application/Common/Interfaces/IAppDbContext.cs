using Microsoft.EntityFrameworkCore;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>Application-layer abstraction over the EF DbContext (keeps handlers DB-agnostic).</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Staff> Staff { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    // Students (tenant-owned) — FR-ADM-STU-*, FR-STU-REG-*
    DbSet<Student> Students { get; }
    DbSet<StudentDevice> StudentDevices { get; }

    // Taxonomy (tenant-owned, dynamic) — FR-PLAT-TAX-001
    DbSet<Grade> Grades { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<Specialization> Specializations { get; }

    // Sessions & content (tenant-owned) — FR-PLAT-SES-*, FR-ADM-SES-*
    DbSet<Session> Sessions { get; }
    DbSet<SessionVideo> SessionVideos { get; }
    DbSet<SessionMaterial> SessionMaterials { get; }

    // Question bank (tenant-owned) — FR-PLAT-QB-*, FR-ADM-QB-*
    DbSet<Question> Questions { get; }

    // Location reference data (global, seeded, read-only) — FR-PLAT-TAX-003
    DbSet<City> Cities { get; }
    DbSet<Region> Regions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a single database transaction, committing on success.
    /// Buffered domain events are dispatched only after the commit. Used by the transaction pipeline
    /// behaviour for <see cref="ITransactionalRequest"/> commands.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> action, CancellationToken cancellationToken = default);
}
