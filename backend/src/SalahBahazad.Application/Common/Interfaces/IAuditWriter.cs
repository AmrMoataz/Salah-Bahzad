namespace SalahBahazad.Application.Common.Interfaces;

/// <summary>
/// Appends an explicit, hash-chained audit entry for actions the SaveChanges interceptor cannot
/// capture automatically:
/// <list type="bullet">
///   <item><b>Read accesses</b> with no entity change — e.g. issuing a signed URL for an ID image,
///   which must be audited (FR-PLAT-AST-003, NFR-PRIV-001/002).</item>
///   <item><b>Anonymous</b> actions with no JWT tenant claim — e.g. student self-registration, where
///   the interceptor (which scopes audit to the JWT tenant) is a no-op (FR-PLAT-AUD-002).</item>
/// </list>
/// Entries written here link into the same tamper-evident chain as interceptor-written rows.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes an explicit audit entry. <see cref="TenantId"/> is supplied for anonymous flows that
/// resolve their tenant outside the JWT; when null the writer uses the current JWT tenant. Actor
/// fields are inferred from the current user (authenticated → "Staff") unless <see cref="ActorType"/>
/// is set (e.g. "Student" for self-registration).
/// </summary>
public sealed record AuditWriteRequest(
    string Action,
    string EntityType,
    Guid EntityId,
    string Summary,
    Guid? TenantId = null,
    string? ActorType = null,
    string? Portal = null);
