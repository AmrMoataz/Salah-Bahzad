namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Publication state of a <see cref="Entities.Session"/> (FR-PLAT-SES-001, FR-ADM-SES-002).
/// Authoring happens in <see cref="Draft"/>; staff <see cref="Published"/> it to make it catalogue-visible
/// (FR-PLAT-SES-008) and may later <see cref="Archived"/> it to retire it without deleting history.
/// </summary>
public enum SessionStatus
{
    /// <summary>Being authored; not visible in the student catalogue.</summary>
    Draft = 0,

    /// <summary>Live and visible in the catalogue / enrollable.</summary>
    Published = 1,

    /// <summary>Retired; hidden from new enrollment but retained for history.</summary>
    Archived = 2,
}
