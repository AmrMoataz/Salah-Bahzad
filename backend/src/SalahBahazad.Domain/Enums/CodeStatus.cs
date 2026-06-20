namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Lifecycle state of a redemption <see cref="Entities.Code"/> (FR-PLAT-COD-001..006). The admin UI labels
/// <see cref="Inactive"/> as "Disabled". A soft-deleted code is hidden by the global query filter rather
/// than carrying a status — "Deleted" is not a queryable state (contract §5).
/// </summary>
public enum CodeStatus
{
    /// <summary>Mintable/redeemable — the only state from which a redemption is allowed.</summary>
    Active = 0,

    /// <summary>Disabled by staff; not redeemable but not deleted (re-enableable).</summary>
    Inactive = 1,

    /// <summary>Already redeemed exactly once (FR-PLAT-COD-003); terminal unless returned by a refund.</summary>
    Used = 2,
}
