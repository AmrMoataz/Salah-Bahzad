namespace SalahBahazad.Domain.Common;

/// <summary>
/// Implemented by entities that participate in audit, attendance, or financial history and therefore
/// must be <b>soft-deleted</b> (FR-PLAT-ROLE-004). A global query filter hides deleted rows by default.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>True once the row is soft-deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Actor that performed the soft-delete.</summary>
    Guid? DeletedById { get; }

    /// <summary>Soft-delete timestamp (UTC).</summary>
    DateTimeOffset? DeletedAtUtc { get; }
}
